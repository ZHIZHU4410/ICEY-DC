import datetime,json,socket,threading,time,os,sys
from pathlib import Path
out=Path(sys.argv[1])
roles={};lock=threading.RLock();journal=(out/'relay.jsonl').open('a',encoding='utf8')
def record(role,line):
    with lock:journal.write(json.dumps(dict(utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),ns=time.perf_counter_ns(),role=role,line=line))+'\n');journal.flush()
def client(s):
    role='UNKNOWN'
    try:
        s.setsockopt(socket.IPPROTO_TCP,socket.TCP_NODELAY,1)
        with s.makefile('r',encoding='ascii') as f:
            for raw in f:
                line=raw.strip();p=line.split('|')
                if len(p)<3 or p[0]!='EP4':continue
                if p[1]=='HELLO':
                    role=p[2]
                    if role not in ['DC','ICEY']:break
                    with lock:roles[role]=s
                if p[1]!='SAMPLE':record(role,line)
                if (role=='DC' and p[1] in ('INPUT','INCOMING','MOBDEAD','MODE','TESTHEAL')) or (role=='ICEY' and p[1] in ('HIT','DEFENSE','KEYMAP','HEALED')):
                    with lock:target=roles.get('ICEY' if role=='DC' else 'DC')
                    if target:target.sendall((line+'\n').encode('ascii'))
    except (OSError,ValueError) as error:record(role,'ERROR '+str(error))
    finally:
        with lock:
            if roles.get(role) is s:roles.pop(role,None)
        s.close()
server=socket.socket();server.bind(('127.0.0.1',42250));server.listen(4)
(out/'relay_pid.txt').write_text(str(os.getpid()),encoding='ascii')
record('SERVER','START 127.0.0.1:42250')
while True:
    s,_=server.accept();threading.Thread(target=client,args=(s,),daemon=True).start()
