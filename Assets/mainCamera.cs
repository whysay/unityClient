using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using System;
using System.IO;
using System.Threading;
using System.Runtime.InteropServices;
using System.Text;

using System.Net.Json;

// State object for receiving data from remote device.
public class StateObject
{
	// Client socket.
	public Socket workSocket = null;
	// Size of receive buffer.
	public const int BufferSize = 60000;
	// Receive buffer.
	public byte[] buffer = new byte[BufferSize];
	public int recvSize = 0;
	// Received data string.
	//public StringBuilder sb = new StringBuilder();
}

public class mainCamera : MonoBehaviour {
	
	//private DataSet _ds;
	public static bool m_isDisconnected = false;
	
	[Serializable]
	
	public struct _TCP_PACKET_H
	{
		public int PacketSize;
		public short Cmd;
	}
	
	
	
	//const int TCP_HEADER_SIZE = Marshal.SizeOf(_TCP_PACKET_H);
	
	//const int MAX_IOCP_BUFFER_SIZE	= 8192;			// IOCP에 사용할 버퍼의 크기
	//const int MAX_SEND_PACKET_SIZE	= 1024;			// 가장 큰 패킷의 크기
	
	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
	struct _TMP_PACKET
	{
		public _TCP_PACKET_H Header;
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 50000)]
		public string str;
	};
	public static void StructToBytes(object obj, ref byte[] packet)
	{
		int size = Marshal.SizeOf(obj);
		packet = new byte[size];
		IntPtr buffer = Marshal.AllocHGlobal(packet.Length + 1);
		Marshal.StructureToPtr(obj, buffer, false); // 사이즈가 1이라도 틀리면 예외발생하니 주의
		Marshal.Copy(buffer, packet, 0, packet.Length);
		Marshal.FreeHGlobal(buffer);
	}
	
	private static bool ReadStruct<T>(byte[] buffer, out T obj) where T : struct
	{
		obj = default(T);
		int size = Marshal.SizeOf(typeof(T));
		if (size > buffer.Length)
		{
			return false;
		}
		//throw new Exception();
		IntPtr ptr = Marshal.AllocHGlobal(size);
		Marshal.Copy(buffer, 0, ptr, size);
		obj = (T)Marshal.PtrToStructure(ptr, typeof(T));
		Marshal.FreeHGlobal(ptr);
		return true;
	}
	
	//---------------------------네트워크----------------------------------
	// The port number for the remote device.
	private const int port = 50000;
	
	// ManualResetEvent instances signal completion.
	private static ManualResetEvent connectDone =
		new ManualResetEvent(false);
	private static ManualResetEvent sendDone =
		new ManualResetEvent(false);
	private static ManualResetEvent receiveDone =
		new ManualResetEvent(false);
	
	// The response from the remote device.
	private static String _response = String.Empty;
	
	private static void Receive(Socket client)
	{
		try
		{
			// Create the state object.
			StateObject state = new StateObject();
			state.workSocket = client;
			
			// Begin receiving the data from the remote device.
			client.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
			                    new AsyncCallback(ReceiveCallback), state);
		}
		catch (Exception e)
		{
			Console.WriteLine(e.ToString());
		}
	}
	
	private static void ReceiveCallback(IAsyncResult ar)
	{
		try
		{
			// Retrieve the state object and the client socket 
			// from the asynchronous state object.
			StateObject state = (StateObject)ar.AsyncState;
			Socket client = state.workSocket;
			
			// Read data from the remote device.
			int bytesRead = client.EndReceive(ar);
			
			if (bytesRead > 0)
			{
				// There might be more data, so store the data received so far.
				//state.sb.Append(Encoding.ASCII.GetString(state.buffer, 0, bytesRead));
				
				// 스트링 버퍼말고 바이트버퍼를 따로 만들어서 패킷 1개 처리 완료될때까지 모아서
				// 처리하도록 제작되야함
				
				//byte[] packetData = Encoding.ASCII.GetBytes(state.sb.ToString());
				byte[] packetData = state.buffer;
				_TCP_PACKET_H recvheader;
				if (ReadStruct<_TCP_PACKET_H>(state.buffer, out recvheader))
				{
					state.recvSize += bytesRead;
					if (state.recvSize < recvheader.PacketSize)
					{
						// 아직 덜받음
						client.BeginReceive(state.buffer, state.recvSize, recvheader.PacketSize - state.recvSize, 0,
						                    new AsyncCallback(ReceiveCallback), state);
						return;
						
					}
					// 패킷헤더 사이즈가 이상하게 나옴 디버깅 필요..
					int strDataSize = recvheader.PacketSize - Marshal.SizeOf(recvheader);
					byte[] strData = new byte[strDataSize];

					Array.Copy(state.buffer, Marshal.SizeOf(recvheader),strData, 0, strDataSize);
					//arrCopy(state.buffer, Marshal.SizeOf(recvheader), strData, 0, strDataSize);
					lock (lockObject)
					{
						packetStr.Enqueue(Encoding.ASCII.GetString(strData));
					}
					//state.sb.Clear();
				}
				
				// Get the rest of the data.
				state.recvSize = 0;
				client.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
				                    new AsyncCallback(ReceiveCallback), state);
				
			}
			else
			{
				// All the data has arrived; put it in response.
				//if (state.sb.Length > 1)
				//{
				//    _response = state.sb.ToString();
				//}
				m_isDisconnected = true;
				// Signal that all bytes have been received.
				receiveDone.Set();
			}
		}
		catch (Exception e)
		{
			m_isDisconnected = true;
			receiveDone.Set();
			//Console.WriteLine(e.ToString());
		}
	}
	
	private static void Send(Socket client, byte[] byteData, int size)
	{
		// Convert the string data to byte data using ASCII encoding.
		// byte[] byteData = Encoding.ASCII.GetBytes(data);
		
		// Begin sending the data to the remote device.
		client.BeginSend(byteData, 0, size, 0,
		                 new AsyncCallback(SendCallback), client);
	}
	
	private static void SendCallback(IAsyncResult ar)
	{
		try
		{
			// Retrieve the socket from the state object.
			Socket client = (Socket)ar.AsyncState;
			
			// Complete sending the data to the remote device.
			int bytesSent = client.EndSend(ar);
			Console.WriteLine("Sent {0} bytes to server.", bytesSent);
			
			// Signal that all bytes have been sent.
			sendDone.Set();
		}
		catch (Exception e)
		{
			Console.WriteLine(e.ToString());
		}
	}
	//---------------------------------------------------------------------
	//static AsynchronousClient _client;
	//TcpClient client = null;
	//static NetworkStream netStream = null;
	static Socket _client;
	static Queue<string> packetStr = new Queue<string>();
	static object lockObject = new object();
	
	public static void NetCallbackFunc()
	{
		while(true)
		{
			Receive(_client);
			receiveDone.WaitOne();
			if (m_isDisconnected)
				break;
		}
	}


	// Use this for initialization
	void Start () {
		//Sample ();
		Connect ();

//		string reqStr;
//		JsonObjectCollection collection = new JsonObjectCollection();
//		collection.Add(new JsonNumericValue("stars", 1));
//		reqStr = collection.ToString();
//		infoReq(3, reqStr);
	}
	
	
	// Update is called once per frame
	void Update () {
		packetProc ();

	}
	
	
	static void Connect() 
	{
		try
		{
			_client = new Socket(AddressFamily.InterNetwork,
			                     SocketType.Stream, ProtocolType.Tcp);
			
			// Connect to the remote endpoint.
			_client.Connect("127.0.0.1", port);
			
			Thread netThread = new Thread(new ThreadStart(NetCallbackFunc));
			netThread.Start();
			
			// 로그인창 띄우기
			//ShowLoginWindow();
		}
		catch (Exception e)
		{
			Console.WriteLine(e.Message);
		}
		finally
		{
			
		}
	}

	private void packetProc()
	{
		if (m_isDisconnected)
		{
			//Close();
			return;
		}
		string curStr;
		lock (lockObject)
		{
			if (0 == packetStr.Count)
				return;
			
			curStr = packetStr.Dequeue();
			
			// code goes here
			JsonTextParser parser = new JsonTextParser();
			
			
			JsonObject obj = parser.Parse(curStr);
			
			JsonObjectCollection col = (JsonObjectCollection)obj;
			
			switch (Convert.ToInt32(col["cmd"].GetValue()))
			{
			case 1:// 로그인결과
			{
				switch (Convert.ToInt32(col["ret"].GetValue()))
				{
				case 1:// 없는유저
				{
//					string reqStr;
//					JsonObjectCollection collection = new JsonObjectCollection();
//					collection.Add(new JsonNumericValue("stars", 1));
//					reqStr = collection.ToString();
//					infoReq(3, reqStr);
				}
					break;
				case 2:// 비번틀림
				{

				}
					break;
				case 0:// 로그인성공
				{
					string reqStr;
					JsonObjectCollection collection = new JsonObjectCollection();
					collection.Add(new JsonNumericValue("stars", 1));
					reqStr = collection.ToString();
					infoReq(3, reqStr);
				}
					break;
				}
			}
				break;
			}//switch
		}
	}

	private void infoReq(short reqType, string inputStr)
	{
		//로그인
		_TMP_PACKET aaa = new _TMP_PACKET();
		aaa.str = inputStr;
		int size = Marshal.SizeOf(aaa.Header);
		if (inputStr == "")
			aaa.Header.PacketSize = (short)(size);
		else
			aaa.Header.PacketSize = (short)(size + Encoding.Unicode.GetBytes(aaa.str).Length + 2);
		aaa.Header.Cmd = reqType;//요청타입
		byte[] byteBuffer = new byte[aaa.Header.PacketSize];
		StructToBytes(aaa, ref byteBuffer);
		
		Send(_client, byteBuffer, aaa.Header.PacketSize);
	}

	void OnGUI() 
	{
		Event e = Event.current;
		if (e.button == 0 && e.isMouse)
		{
			Debug.Log ("Left Click & send");
			string reqStr;
			JsonObjectCollection collection = new JsonObjectCollection ();
			collection.Add (new JsonNumericValue ("stars", 1));
			reqStr = collection.ToString ();
			infoReq (3, reqStr);
		}
		else
			if (e.button == 1)
				Debug.Log("Right Click");
		else
			if (e.button == 2)
				Debug.Log("Middle Click");
		else
			if (e.button > 2)
				Debug.Log("Another button in the mouse clicked");
		
	}
	
}
