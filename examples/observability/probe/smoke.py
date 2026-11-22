import fcntl, json, os, re, socket, struct, subprocess, time
from pathlib import Path
from urllib.error import HTTPError
from urllib.request import ProxyHandler, Request, build_opener
assert os.readlink("/proc/self/ns/net")!=os.environ["QAAS_PARENT_NET_NS"]
with socket.socket(socket.AF_INET,socket.SOCK_DGRAM) as s:
    req=struct.pack("16sh22x",b"lo",0)
    flags=struct.unpack_from("h",fcntl.ioctl(s,0x8913,req),16)[0]
    fcntl.ioctl(s,0x8914,struct.pack("16sh22x",b"lo",flags|1))
assert [x.split(":",1)[0].strip() for x in Path("/proc/net/dev").read_text().splitlines()[2:]]==["lo"]
http=build_opener(ProxyHandler({}))
def get(port, path, method="GET"):
    q=Request(f"http://127.0.0.1:{port}"+path,method=method)
    try:
        with http.open(q,timeout=2) as resp: return resp.status,resp.read().decode()
    except HTTPError as err:
        return err.code,err.read().decode()
def wait_port(port):
    for _ in range(60):
        try:
            with socket.create_connection(("127.0.0.1",port),timeout=.2): return
        except OSError: time.sleep(.15)
    raise RuntimeError("listener unavailable")
def dump_scrape(port):
    status,text=get(port,"/metrics")
    assert status==200,(status,text)
    return text
def values(s):
    return {m.group(1):float(m.group(2)) for line in s.replace('{otel_scope_name="QaaS.Mocker.Http"}','').replace('otel_scope_name="QaaS.Mocker.Http",','').splitlines()
       if (m:=re.match(r'^(qaas_mocker_\S+)\s+([^\s]+)',line))}
def run(enabled):
    with open("host-"+str(enabled)+".log","w") as f:
        args=[os.environ.get("DOTNET_BINARY","dotnet"),"bin/Debug/net10.0/Smoke.dll","16920","16921"]
        if not enabled: args.append("disabled")
        p=subprocess.Popen(args,stdout=f,stderr=subprocess.STDOUT)
        try:
            wait_port(16920); wait_port(16921)
            initial=dump_scrape(16921)
            checks=[("GET","/matched",404),("GET","/missing",200),("POST","/matched",200),
                    ("GET","/broken",202),("BREW","/matched",500)]
            got=[(method,path,get(16920,path,method)[0]) for method,path,_ in checks]
            assert got==checks,(got,checks)
            # The beta exporter caches responses briefly.
            time.sleep(1)
            after=dump_scrape(16921)
            Path("scrape-"+str(enabled)+".txt").write_text("\n".join(line for line in after.splitlines() if "qaas_mocker_" in line)+"\n")
            obs=values(after)
            if enabled:
                pfx="qaas_mocker_request_duration_seconds"
                base={"qaas_mocker_requests_accepted_total":5,
                  **{f'qaas_mocker_requests_completed_total{{outcome="{k}"}}':v
                     for k,v in {"hit":1,"miss":2,"error":2}.items()},
                  pfx+"_count":5,pfx+'_bucket{le="+Inf"}':5}
                for k,v in base.items(): assert obs[k]==v,(k,v,obs)
                assert len(obs)==15,obs
                assert set(re.findall(r'le="([^"]+)"',"\n".join(obs)))=={".05","0.05","0.1","0.25","0.5","1","2.5","5","15","+Inf"}-{".05"},obs
                initial_obs=values(initial)
                assert all(initial_obs[k]==0 for k in base if "_duration_" not in k),initial_obs
            else: assert not obs,obs
            return {"enabled":enabled,"http":got,"series":len(obs),"initial":values(initial),"final_product_series":obs}
        finally:
            p.terminate()
            try:p.wait(5)
            except subprocess.TimeoutExpired:p.kill();p.wait(5)
result={"probe":"actual Mocker HttpServer and optional OpenTelemetry .NET Prometheus ASP.NET Core bridge, loopback-only",
 "results":[run(False),run(True)]}
Path("local-result.json").write_text(json.dumps(result,indent=2)+"\n")
print(json.dumps(result))
