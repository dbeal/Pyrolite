/* part of Pyrolite, by Irmen de Jong (irmen@razorvine.net) */

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Reflection;

namespace Razorvine.Pyro
{

/// <summary>
/// Proxy for Pyro objects.
/// </summary>
[Serializable]
public class PyroProxy : IDisposable {

	public string hostname {get;set;}
	public int port {get;set;}
	public string objectid {get;set;}

	private ushort sequenceNr = 0;
	private TcpClient sock;
	private NetworkStream sock_stream;

	/**
	 * No-args constructor for (un)pickling support
	 */
	public PyroProxy() {
	}

	/**
	 * Create a proxy for the remote Pyro object denoted by the uri
	 */
	public PyroProxy(PyroURI uri) : this(uri.host, uri.port, uri.objectid) {
	}

	/**
	 * Create a proxy for the remote Pyro object on the given host and port, with the given objectid/name.
	 */
	public PyroProxy(string hostname, int port, string objectid) {
		this.hostname = hostname;
		this.port = port;
		this.objectid = objectid;
	}

	/**
	 * Release resources when descructed
	 */
	~PyroProxy() {
		this.close();
	}

	public void Dispose() {
		close();
	}
	
	/**
	 * (re)connect the proxy to the remote Pyro daemon.
	 */
	protected void connect() {
		if (sock == null) {
			sock = new TcpClient();
			sock.Connect(hostname,port);
			sock.NoDelay=true;
			sock_stream=sock.GetStream();
			sequenceNr = 0;
			handshake();
		}
	}

	/**
	 * Call a method on the remote Pyro object this proxy is for.
	 * @param method the name of the method you want to call
	 * @param arguments zero or more arguments for the remote method
	 * @return the result Object from the remote method call (can be anything, you need to typecast/introspect yourself).
	 */
	public object call(string method, params object[] arguments) {
		return call(method, 0, arguments);
	}

	/**
	 * Call a method on the remote Pyro object this proxy is for, using Oneway call semantics (return immediately).
	 * @param method the name of the method you want to call
	 * @param arguments zero or more arguments for the remote method
	 */
	public void call_oneway(string method, params object[] arguments) {
		call(method, Message.FLAGS_ONEWAY, arguments);
	}

	/**
	 * Internal call method to actually perform the Pyro method call and process the result.
	 */
	private object call(string method, ushort flags, params object[] parameters) {
		lock(this) {
			connect();
			unchecked {
			    sequenceNr++;        // unchecked so this ushort wraps around 0-65535 instead of raising an OverflowException
			}
		}
		if (parameters == null)
			parameters = new object[] {};
		PyroSerializer ser = PyroSerializer.GetFor(Config.SERIALIZER);
		byte[] pickle = ser.serializeCall(objectid, method, parameters, new Dictionary<string,object>(0));
		var msg = new Message(Message.MSG_INVOKE, pickle, ser.serializer_id, flags, sequenceNr, null);
		Message resultmsg;
		lock (this.sock) {
			IOUtil.send(sock_stream, msg.to_bytes());
			if(Config.MSG_TRACE_DIR!=null) {
				Message.TraceMessageSend(sequenceNr, msg.get_header_bytes(), msg.get_annotations_bytes(), msg.data);
			}
			pickle = null;

			if ((flags & Message.FLAGS_ONEWAY) != 0)
				return null;

			resultmsg = Message.recv(sock_stream, new ushort[]{Message.MSG_RESULT});
		}
		if (resultmsg.seq != sequenceNr) {
			throw new PyroException("result msg out of sync");
		}
		if ((resultmsg.flags & Message.FLAGS_COMPRESSED) != 0) {
			// we need to skip the first 2 bytes in the buffer due to a tiny mismatch between zlib-written 
			// data and the deflate data bytes that .net expects.
			// See http://www.chiramattel.com/george/blog/2007/09/09/deflatestream-block-length-does-not-match.html
			using(MemoryStream compressed=new MemoryStream(resultmsg.data, 2, resultmsg.data.Length-2, false)) {
				using(DeflateStream decompresser=new DeflateStream(compressed, CompressionMode.Decompress)) {
					MemoryStream bos = new MemoryStream(resultmsg.data.Length);
	        		byte[] buffer = new byte[4096];
	        		int numRead;
	        		while ((numRead = decompresser.Read(buffer, 0, buffer.Length)) != 0) {
	        		    bos.Write(buffer, 0, numRead);
	        		}
	        		resultmsg.data=bos.ToArray();
				}
			}
		}

		if ((resultmsg.flags & Message.FLAGS_EXCEPTION) != 0) {
			Exception rx = (Exception) ser.deserializeData(resultmsg.data);
			if (rx is PyroException) {
				throw (PyroException) rx;
			} else {
				PyroException px = new PyroException("remote exception occurred", rx);
				PropertyInfo remotetbProperty=rx.GetType().GetProperty("_pyroTraceback");
				if(remotetbProperty!=null) {
					string remotetb=(string)remotetbProperty.GetValue(rx,null);
					px._pyroTraceback=remotetb;
				}
				throw px;
			}
		}
		
		return ser.deserializeData(resultmsg.data);
	}

	/**
	 * Close the network connection of this Proxy.
	 * If you re-use the proxy, it will automatically reconnect.
	 */
	public void close() {
		if (this.sock != null) {
			if(this.sock_stream!=null) this.sock_stream.Close();
			this.sock.Client.Close();
			this.sock.Close();
			this.sock=null;
			this.sock_stream=null;
		}
	}

	/**
	 * Perform the Pyro protocol connection handshake with the Pyro daemon.
	 */
	protected void handshake() {
		// do connection handshake
		Message.recv(sock_stream, new ushort[]{Message.MSG_CONNECTOK});
		// message data is ignored for now, should be 'ok' :)
	}

	/**
	 * called by the Unpickler to restore state.
	 * args: pyroUri, pyroOneway(hashset), pyroTimeout
	 */
	public void __setstate__(object[] args) {
		PyroURI uri=(PyroURI)args[0];
		// the only thing we need here is the uri.
		this.hostname=uri.host;
		this.port=uri.port;
		this.objectid=uri.objectid;
		this.sock=null;
		this.sock_stream=null;
	}	
}

}
