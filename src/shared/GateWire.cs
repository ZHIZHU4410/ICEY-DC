using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace EarlyPlaytest
{
    // Receive from the socket directly: StreamReader can hide a second buffered line
    // while TcpClient.Available is zero, delaying input until an unrelated packet arrives.
    public sealed class GateWire : IDisposable
    {
        readonly string role;
        readonly Queue<string> incoming=new Queue<string>(),outgoing=new Queue<string>();
        readonly object sync=new object();
        Thread worker;TcpClient client;volatile bool running;
        public volatile bool Connected;
        public GateWire(string role){this.role=role;}
        public void Start(){running=true;worker=new Thread(Loop);worker.IsBackground=true;worker.Start();}
        public void Send(string line){lock(sync){if(outgoing.Count>=128)throw new IOException("bridge outgoing queue overflow; combat unavailable");outgoing.Enqueue(line);}}
        public string Receive(){lock(sync){return incoming.Count==0?null:incoming.Dequeue();}}
        void Accept(string line){if(!line.StartsWith("EP4|",StringComparison.Ordinal))return;lock(sync){if(incoming.Count>=128)throw new IOException("incoming overflow");incoming.Enqueue(line);}}
        void Loop(){while(running){try{
            client=new TcpClient();client.NoDelay=true;client.Connect("127.0.0.1",42250);
            using(NetworkStream ns=client.GetStream())using(StreamWriter writer=new StreamWriter(ns)){
                byte[] bytes=new byte[4096];char[] chars=new char[4096];Decoder decoder=Encoding.UTF8.GetDecoder();StringBuilder line=new StringBuilder();
                writer.AutoFlush=true;writer.WriteLine("EP4|HELLO|"+role);Connected=true;
                while(running){
                    // Drain a bounded burst in order; never wait one millisecond per queued event.
                    for(int n=0;n<32;n++){string send=null;lock(sync){if(outgoing.Count>0)send=outgoing.Dequeue();}if(send==null)break;writer.WriteLine(send);}
                    int available=client.Available;
                    if(available>0){
                        int count=ns.Read(bytes,0,Math.Min(available,bytes.Length));if(count==0)break;
                        int decoded=decoder.GetChars(bytes,0,count,chars,0);
                        for(int i=0;i<decoded;i++){
                            char c=chars[i];if(c=='\n'){Accept(line.ToString().TrimEnd('\r'));line.Length=0;}
                            else {line.Append(c);if(line.Length>16384)throw new IOException("bridge line too long");}
                        }
                    }else if(client.Client.Poll(0,SelectMode.SelectRead))break;
                    Thread.Sleep(1);
                }
            }
        }catch(Exception){}finally{Connected=false;if(client!=null)client.Close();}if(running)Thread.Sleep(200);}}
        public void Dispose(){running=false;if(client!=null)client.Close();}
    }
}
