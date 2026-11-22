"""Exercise real product HTTP servers and the optional test-only Prometheus exporter."""
import concurrent.futures, fcntl, json, os, re, socket, struct, subprocess, time
from pathlib import Path
from urllib.error import HTTPError
from urllib.request import ProxyHandler, Request, build_opener

assert os.readlink("/proc/self/ns/net") != os.environ["QAAS_PARENT_NET_NS"]
with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as sock:
    command = struct.pack("16sh22x", b"lo", 0)
    flags = struct.unpack_from("h", fcntl.ioctl(sock, 0x8913, command), 16)[0]
    fcntl.ioctl(sock, 0x8914, struct.pack("16sh22x", b"lo", flags | 1))
assert [row.split(":", 1)[0].strip() for row in Path("/proc/net/dev").read_text().splitlines()[2:]] == ["lo"]
assert not [row for row in Path("/proc/net/route").read_text().splitlines()[1:] if row.split()[0] != "lo"]

HTTP = build_opener(ProxyHandler({}))
A, B, C, EXPORTER = 16920, 16922, 16923, 16921
ACCEPTED = "qaas_mocker_requests_accepted_total"
COMPLETED = "qaas_mocker_requests_completed_total"
DURATION = "qaas_mocker_request_duration_seconds"
BOUNDS = ("0.05", "0.1", "0.25", "0.5", "1", "2.5", "5", "15", "+Inf")

def request(port, path, method="GET"):
    req = Request(f"http://127.0.0.1:{port}{path}", method=method)
    try:
        with HTTP.open(req, timeout=4) as res:
            return res.status, res.read().decode()
    except HTTPError as err:
        return err.code, err.read().decode()

def wait_port(port):
    for _ in range(80):
        try:
            with socket.create_connection(("127.0.0.1", port), timeout=.2):
                return
        except OSError:
            time.sleep(.1)
    raise RuntimeError(f"fixture port {port} unavailable")

def scrape():
    code, text = request(EXPORTER, "/metrics")
    assert code == 200
    values = {}
    for line in text.splitlines():
        if not (m := re.match(r'^(qaas_mocker_\w+)\{([^}]+)\}\s+(\S+)', line)):
            assert not line.startswith("qaas_mocker_"), line
            continue
        labels = dict(re.findall(r'(\w+)="([^"]*)"', m[2]))
        assert labels.pop("otel_scope_name", None) == "QaaS.Mocker.Http"
        assert labels["metric_slot"] in ("1", "2") and set(labels) <= {"metric_slot", "outcome", "le"}, labels
        key = m[1] + "{" + ",".join(f'{k}="{v}"' for k, v in sorted(labels.items())) + "}"
        assert key not in values
        values[key] = float(m[3])
    return values

def keys(slot):
    return {
      ACCEPTED + f'{{metric_slot="{slot}"}}': 0,
      **{COMPLETED + f'{{metric_slot="{slot}",outcome="{o}"}}':0 for o in ("hit", "miss", "error")}
    }

def scenario(enabled, name, run_traffic=True):
    args = [os.environ.get("DOTNET_BINARY", "dotnet"), "bin/Debug/net10.0/Smoke.dll",
            str(A), str(EXPORTER), str(B), str(C)]
    if not enabled:
        args.append("disabled")
    with open(f"host-{name}.log", "w") as out:
        process = subprocess.Popen(args, stdin=subprocess.DEVNULL, stdout=out, stderr=subprocess.STDOUT)
        try:
            for port in (A, B, C, EXPORTER):
                wait_port(port)
            initial = scrape()
            assert initial == (keys(1) | keys(2) if enabled else {}), initial
            if not run_traffic:
                return {"initial_product_series": initial}
            rejection = {}
            checks = [
                (A, "GET", "/matched", 404), (A, "GET", "/matched", 404),
                (B, "GET", "/missing", 200), (B, "GET", "/broken", 202),
                (B, "BREW", "/matched", 500),
                (C, "POST", "/matched", 200), (C, "GET", "/matched", 404),
            ]
            def send(test):
                port,method,path,status = test
                result = request(port,path,method)
                assert result[0] == status, (test,result)
                return {"fixture_server":{A:"A",B:"B",C:"C-disabled"}[port],
                        "method":method,"path":path,"status":result[0],"body":result[1]}
            with concurrent.futures.ThreadPoolExecutor(max_workers=7) as pool:
                responses = list(pool.map(send,checks))
            if enabled:
                rejection["duplicate_slot"] = request(EXPORTER,"/probe/duplicate","POST")[0]
                rejection["invalid_slot"] = request(EXPORTER,"/probe/invalid","POST")[0]
                assert rejection == {"duplicate_slot":409,"invalid_slot":400}, rejection
            # A was already serving before rejected instrumentation changes and still serves.
            responses.append(send((A, "GET", "/matched", 404)))
            time.sleep(1.15)  # allow a fresh snapshot beyond exporter caching
            obs = scrape()
            expected = {}
            if enabled:
                for slot, hits, misses, errors in ((1,3,0,0),(2,0,1,2)):
                    tag = f'{{metric_slot="{slot}"}}'
                    expected[ACCEPTED + tag] = 3
                    for outcome, count in (("hit",hits),("miss",misses),("error",errors)):
                        expected[COMPLETED + f'{{metric_slot="{slot}",outcome="{outcome}"}}'] = count
                    expected[DURATION + "_count" + tag] = 3
                    expected[DURATION + f'_bucket{{le="+Inf",metric_slot="{slot}"}}'] = 3
                    assert {b for b in BOUNDS if DURATION + f'_bucket{{le="{b}",metric_slot="{slot}"}}' in obs} == set(BOUNDS)
                assert len(obs) == 30 and all(obs.get(key)==value for key,value in expected.items()), obs
                assert sum(v for k,v in obs.items() if k.startswith(ACCEPTED)) == 6
            else:
                assert not obs, obs
            return {"http":responses,"rejected_config":rejection,"series":len(obs),
                    "initial_product_series":initial,"final_product_series":obs}
        finally:
            process.terminate()
            try:
                process.wait(5)
            except subprocess.TimeoutExpired:
                process.kill(); process.wait(5)

disabled = scenario(False,"disabled")
enabled = scenario(True,"enabled")
reset = scenario(True,"restart",run_traffic=False)
assert disabled["http"] == enabled["http"]
proof = {
  "fixture":"actual concurrent Mocker HTTP servers and optional .NET Prometheus bridge; loopback only",
  "disabled_mode":disabled,"enabled_mode":enabled,"new_process_can_reuse_slots_with_zero_counters":reset,
  "http_parity":True,"owned_processes_stopped":True
}
Path("local-result.json").write_text(json.dumps(proof,indent=2)+"\n")
print(json.dumps({"http_parity":True,"disabled_series":disabled["series"],"enabled_series":enabled["series"],
                  "reset_counters":len(reset["initial_product_series"]),"rejected_config":enabled["rejected_config"]}))
