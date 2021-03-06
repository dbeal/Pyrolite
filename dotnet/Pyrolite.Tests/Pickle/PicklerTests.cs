/* part of Pyrolite, by Irmen de Jong (irmen@razorvine.net) */

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Text;
using NUnit.Framework;
using Razorvine.Pickle;

namespace Pyrolite.Tests.Pickle
{


/// <summary>
/// Unit tests for the pickler. 
/// </summary>
[TestFixture]
public class PicklerTests {

	[TestFixtureSetUp]
	public void setUp() {
	}

	[TestFixtureTearDown]
	public void tearDown() {
	}


	byte[] B(string s) {
		return B(PickleUtils.str2bytes(s));
	}
	
	byte[] B(byte[] bytes) {
		byte[] result=new byte[bytes.Length+3];
		result[0]=Opcodes.PROTO;
		result[1]=2;
		result[result.Length-1]=Opcodes.STOP;
		Array.Copy(bytes,0,result,2,bytes.Length);
		return result;
	}
	
	string S(byte[] pickled) {
		return PickleUtils.rawStringFromBytes(pickled);
	}

	public enum DayEnum {
	    SUNDAY, MONDAY, TUESDAY, WEDNESDAY, 
	    THURSDAY, FRIDAY, SATURDAY 
	};
	
	[Test]
	public void testSinglePrimitives() {
		// protocol level 2
		Pickler p=new Pickler(false);
		byte[] o=p.dumps(null);	// none
		Assert.AreEqual(B("N"), o); 
		o=p.dumps('@');  // char --> string
		Assert.AreEqual(B("X\u0001\u0000\u0000\u0000@"), o);
		o=p.dumps(true);	// bool
		Assert.AreEqual(B("\u0088"), o);
		o=p.dumps("hello");      // unicode string
		Assert.AreEqual(B("X\u0005\u0000\u0000\u0000hello"), o);
		o=p.dumps("hello\u20ac");      // unicode string with non ascii
		Assert.AreEqual(B("X\u0008\u0000\u0000\u0000hello\u00e2\u0082\u00ac"), o);
		o=p.dumps((byte)'@');
		Assert.AreEqual(B("K@"), o);
		o=p.dumps((sbyte)-40);
		Assert.AreEqual( B(new byte[]{(byte)'J',0xd8,0xff,0xff,0xff}), o);
		o=p.dumps((short)-0x1234);
		Assert.AreEqual(B("J\u00cc\u00ed\u00ff\u00ff"), o);
		o=p.dumps((ushort)0xf234);
		Assert.AreEqual(B("M\u0034\u00f2"), o);
		o=p.dumps((int)-0x12345678);
		Assert.AreEqual(B("J\u0088\u00a9\u00cb\u00ed"), o);
		o=p.dumps((uint)0x12345678);
		Assert.AreEqual(B(new byte[]{(byte)'J', 0x78, 0x56, 0x34, 0x12}), o);
		o=p.dumps((uint)0xf2345678);
		Assert.AreEqual(B("I4063516280\n"), o);
		o=p.dumps((long)0x12345678abcdefL);
		Assert.AreEqual(B("I5124095577148911\n"), o);
		o=p.dumps(1234.5678d);
		Assert.AreEqual(B(new byte[] {(byte)'G',0x40,0x93,0x4a,0x45,0x6d,0x5c,0xfa,0xad}), o);
		o=p.dumps(1234.5f);
		Assert.AreEqual(B(new byte[] {(byte)'G',0x40,0x93,0x4a,0,0,0,0,0}), o);
		o=p.dumps(1234.9876543210987654321m);
		Assert.AreEqual(B("cdecimal\nDecimal\nX\u0018\u0000\u0000\u00001234.9876543210987654321\u0085R"), o);
		
		DayEnum day=DayEnum.WEDNESDAY;
		o=p.dumps(day);	// enum is returned as just a string representing the value
		Assert.AreEqual(B("X\u0009\u0000\u0000\u0000WEDNESDAY"),o);
	}
	
	[Test]
	public void testArrays() 
	{
		Pickler p = new Pickler(false);
		byte[] o;
		o=p.dumps(new string[] {});
		Assert.AreEqual(B(")"), o);
		o=p.dumps(new string[] {"abc"});
		Assert.AreEqual(B("X\u0003\u0000\u0000\u0000abc\u0085"), o);
		o=p.dumps(new string[] {"abc","def"});
		Assert.AreEqual(B("X\u0003\u0000\u0000\u0000abcX\u0003\u0000\u0000\u0000def\u0086"), o);
		o=p.dumps(new string[] {"abc","def","ghi"});
		Assert.AreEqual(B("X\u0003\u0000\u0000\u0000abcX\u0003\u0000\u0000\u0000defX\u0003\u0000\u0000\u0000ghi\u0087"), o);
		o=p.dumps(new string[] {"abc","def","ghi","jkl"});
		Assert.AreEqual(B("(X\u0003\u0000\u0000\u0000abcX\u0003\u0000\u0000\u0000defX\u0003\u0000\u0000\u0000ghiX\u0003\u0000\u0000\u0000jklt"), o);

		o=p.dumps(new char[] {'A','B','C'});
		Assert.AreEqual(B("X\u0003\u0000\u0000\u0000ABC"), o);

		o=p.dumps(new bool[] {true,false,true});
		Assert.AreEqual(B("\u0088\u0089\u0088\u0087"), o);

		o=p.dumps(new byte[] {1,2,3});
		Assert.AreEqual(B("c__builtin__\nbytearray\nX\u0003\u0000\u0000\u0000\u0001\u0002\u0003X\u0007\u0000\u0000\u0000latin-1\u0086R"), o);

		o=p.dumps(new int[] {1,2,3});
		Assert.AreEqual(B("carray\narray\nU\u0001i](K\u0001K\u0002K\u0003e\u0086R"), o);

		o=p.dumps(new double[] {1.1,2.2,3.3});
		Assert.AreEqual(B("carray\narray\nU\u0001d](G?\u00f1\u0099\u0099\u0099\u0099\u0099\u009aG@\u0001\u0099\u0099\u0099\u0099\u0099\u009aG@\nffffffe\u0086R"), o);
	}
	
	[Test]
	[ExpectedException(ExpectedException=typeof(PickleException), ExpectedMessage="recursive array not supported, use list")]
	public void TestRecursiveArray2()
	{
		Pickler p = new Pickler(false);
		object[] a = new object[] { "hello", "placeholder" };
		a[1] = a; // make it recursive
		p.dumps(a);
	}
	
	[Test]
	[ExpectedException(ExpectedException=typeof(PickleException), ExpectedMessage="recursive array not supported, use list")]
	public void TestRecursiveArray6()
	{
		Pickler p = new Pickler(false);
		object[] a = new object[] { "a","b","c","d","e","f" };
		a[5] = a; // make it recursive
		p.dumps(a);
	}

	[Test]
	public void testDates() 
	{
		Pickler p=new Pickler(false);
		Unpickler u=new Unpickler();
			
		DateTime date=new DateTime(2011,12,31,14,33,59);
		byte[] o = p.dumps(date);
		Object unpickled=u.loads(o);
		Assert.AreEqual(date, unpickled);

		date=new DateTime(2011,12,31,14,33,59,456);
		o=p.dumps(date);
		unpickled=u.loads(o);
		Assert.AreEqual(date, unpickled);
	}
	
	[Test]
	public void testTimes() 
	{
		Pickler p=new Pickler(false);
		Unpickler u=new Unpickler();
		
		TimeSpan ts=new TimeSpan(2, 0, 0, 7000, 456);
		byte[] o = p.dumps(ts);
		object unpickled=u.loads(o);
		Assert.AreEqual(ts,unpickled);
		Assert.AreEqual(B("cdatetime\ntimedelta\nK\u0002MX\u001bJ@\u00f5\u0006\u0000\u0087R"), o);
	}

	[Test]
	public void testSets() 
	{
		byte[] o;
		Pickler p=new Pickler(false);
		Unpickler up=new Unpickler();

		var intset=new HashSet<int>();
		intset.Add(1);
		intset.Add(2);
		intset.Add(3);
		o=p.dumps(intset);
		HashSet<object> resultset=(HashSet<object>)up.loads(o);
		AssertUtils.AssertEqual(intset, resultset);

		HashSet<string> stringset=new HashSet<string>();
		stringset.Add("A");
		stringset.Add("B");
		stringset.Add("C");
		o=p.dumps(stringset);
		resultset=(HashSet<object>)up.loads(o);
		AssertUtils.AssertEqual(stringset, resultset);
	}

	[Test]
	public void testMappings() 
	{
		byte[] o;
		Pickler p=new Pickler(false);
		Unpickler pu=new Unpickler();
		var intmap=new Dictionary<int,int>();
		intmap.Add(1, 11);
		intmap.Add(2, 22);
		intmap.Add(3, 33);
		o=p.dumps(intmap);
		Hashtable resultmap=(Hashtable)pu.loads(o);
		AssertUtils.AssertEqual(intmap, resultmap);

		var stringmap=new Dictionary<string,string>();
		stringmap.Add("A", "1");
		stringmap.Add("B", "2");
		stringmap.Add("C", "3");
		o=p.dumps(stringmap);
		resultmap=(Hashtable)pu.loads(o);
		AssertUtils.AssertEqual(stringmap, resultmap);
		
		Hashtable table=new Hashtable();
		table.Add(1,11);
		table.Add(2,22);
		table.Add(3,33);
		o=p.dumps(table);
		resultmap=(Hashtable)pu.loads(o);
		AssertUtils.AssertEqual(table, resultmap);
	}
	
	[Test]
	public void testLists()  
	{
		byte[] o;
		Pickler p=new Pickler(false);
		
		IList list=new ArrayList();
		list.Add(1);
		list.Add("abc");
		list.Add(null);
		o=p.dumps(list);
		Assert.AreEqual(B("](K\u0001X\u0003\u0000\u0000\u0000abcNe"), o);
		
		IList<object> ilist=new List<object>();
		ilist.Add(1);
		ilist.Add("abc");
		ilist.Add(null);
		o=p.dumps(ilist);
		Assert.AreEqual(B("](K\u0001X\u0003\u0000\u0000\u0000abcNe"), o);

		Stack<int> stack=new Stack<int>();
		stack.Push(1);
		stack.Push(2);
		stack.Push(3);
		o=p.dumps(stack);
		Assert.AreEqual(B("](K\u0003K\u0002K\u0001e"), o);
		
		var queue=new Queue<int>();
		queue.Enqueue(1);
		queue.Enqueue(2);
		queue.Enqueue(3);
		o=p.dumps(queue);
		Assert.AreEqual(B("](K\u0001K\u0002K\u0003e"), o);
 	}

	[Test]
	public void testMemoization()  
	{
		byte[] o;
		Pickler p=new Pickler();
		
		string reused = "reused";
		string another = "another";
		IList list=new ArrayList();
		IList sublist = new ArrayList();
		sublist.Add(reused);
		sublist.Add(reused);
		sublist.Add(another);
		list.Add(reused);
		list.Add(reused);
		list.Add(another);
		list.Add(sublist);
		o=p.dumps(list);
		Assert.AreEqual("\x80\x02]q\x00(X\x06\x00\x00\x0000reusedq\x01h\x01X\x07\x00\x00\x0000anotherq\x02]q\x03(h\x01h\x01h\x0002ee.", S(o));
		
		Unpickler u = new Unpickler();
		ArrayList data = (ArrayList) u.loads(o);
		Assert.AreEqual(4, data.Count);
		string s1 = (string) data[0];
		string s2 = (string) data[1];
		string s3 = (string) data[2];
		data = (ArrayList) data[3];
		string s4 = (string) data[0];
		string s5 = (string) data[1];
		string s6 = (string) data[2];
		Assert.AreEqual("reused", s1);
		Assert.AreEqual("another", s3);
		Assert.AreSame(s1, s2);
		Assert.AreSame(s3, s6);
		Assert.AreSame(s1, s4);
		Assert.AreSame(s1, s5);
	}
		
	[Test]
	[ExpectedException(ExpectedException=typeof(StackOverflowException))]
	public void testMemoizationRecursiveNoMemo()  
	{
		byte[] o;
		Pickler p=new Pickler(false);
		
		string reused = "reused";
		IList list=new ArrayList();
		list.Add(reused);
		list.Add(reused);
		list.Add(list);
		o=p.dumps(list);
	}

	[Test]
	public void testMemoizationRecursiveMemo()  
	{
		byte[] o;
		Pickler p=new Pickler();
		
		// self-referencing list
		string reused = "reused";
		IList list=new ArrayList();
		list.Add(reused);
		list.Add(reused);
		list.Add(list);
		o=p.dumps(list);
		Assert.AreEqual("\x80\x02]q\x00(X\x06\x00\x00\x0000reusedq\x01h\x01h\x0000e.", S(o));

		Unpickler u = new Unpickler();
		ArrayList data = (ArrayList) u.loads(o);
		Assert.AreEqual(3, data.Count);
		string s1 = (string) data[0];
		string s2 = (string) data[1];
		ArrayList data2 = (ArrayList) data[2];
		Assert.AreEqual("reused", s1);
		Assert.AreSame(s1, s2);
		Assert.AreSame(data, data2);
		Assert.AreSame(data[0], data2[0]);
		
		// self-referencing hashtable
		Hashtable h = new Hashtable();
		h["myself"] = h;
		o=p.dumps(h);
		Assert.AreEqual("\x80\x02}q\x00(X\x06\x00\x00\x0000myselfq\x01h\x0000u.", S(o));
		Hashtable h2 = (Hashtable) u.loads(o);
		Assert.AreEqual(1, h2.Count);
		Assert.AreSame(h2, h2["myself"]);
	}

	public class Person {
		
		public string Name {get;set;}
		public bool Deceased {get;set;}
		public int[] Values {get;set;}
				
		public Person(string name, bool deceased, int[] values) {
	    	this.Name=name;
	    	this.Deceased=deceased;
	    	this.Values = values;
	    }
	}
	
	public class Relative : Person {
		public string Relation{get;set;}

		public Relative(string name, bool deceased, int[] values) : base(name, deceased, values) {
			Relation="unspecified";
		}
	}


	[Test]
	public void testClass() 
	{
		Pickler p=new Pickler(false);
		Unpickler pu=new Unpickler();
		byte[] o;
		Relative person=new Relative("Tupac",true, new int[] {3,4,5});
		o=p.dumps(person);
		Hashtable map=(Hashtable) pu.loads(o);
		
		Assert.AreEqual(5, map.Count);
		Assert.AreEqual("Pyrolite.Tests.Pickle.PicklerTests+Relative", map["__class__"]);
		Assert.AreEqual("Tupac", map["Name"]);
		Assert.AreEqual("unspecified", map["Relation"]);
		Assert.AreEqual(true, map["Deceased"]);
		Assert.AreEqual(new int[] {3,4,5}, map["Values"]);
	}
	
	class NotABean {
		public int x;
	}

	[Test, ExpectedException(typeof(PickleException))]
	public void testFailure()
	{
		NotABean notabean=new NotABean();
		notabean.x=42;
		Pickler p=new Pickler(false);
		p.dumps(notabean);
	}
	
	class CustomClass {
		public int x=42;
	}
	class CustomClassPickler : IObjectPickler {
		public void pickle(object o, Stream outs, Pickler currentpickler)  {
			CustomClass c=(CustomClass)o;
			currentpickler.save("customclassint="+c.x);		// write a string representation
		}
	}
	
	[Test]
	public void testCustomPickler() 
	{
		Pickler.registerCustomPickler(typeof(CustomClass), new CustomClassPickler());
		CustomClass c=new CustomClass();
		Pickler p=new Pickler(false);
		byte[] ser = p.dumps(c);
		
		Unpickler u = new Unpickler();
		string x = (string) u.loads(ser);
		Assert.AreEqual("customclassint=42", x);
	}
	
	[Test]
	public void testAnonType()
	{
		var x = new { Name="Harry", Country="UK", Age=34 };
		Pickler p = new Pickler();
		byte[] ser = p.dumps(x);
		
		Unpickler u = new Unpickler();
		object y = u.loads(ser);
		IDictionary dict = (IDictionary) y;
		Assert.AreEqual(3, dict.Count);
		Assert.AreEqual(34, dict["Age"]);
		Assert.IsFalse(dict.Contains("__class__"));
	}
}

}
