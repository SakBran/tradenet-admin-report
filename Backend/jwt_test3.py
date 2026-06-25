import hmac, hashlib, base64, json, urllib.request, ssl, time
def b64(b): return base64.urlsafe_b64encode(b).rstrip(b'=')
key = b"This is my supper secret key for jwt"
hdr = b64(json.dumps({"alg":"HS256","typ":"JWT"}).encode())
now=int(time.time())
payload=b64(json.dumps({"iss":"https://sakbran.github.io","aud":"sakbran.github.io","sub":"t","name":"t","nameid":"1","iat":now,"exp":now+3600}).encode())
sig=b64(hmac.new(key,hdr+b"."+payload,hashlib.sha256).digest())
token=(hdr+b"."+payload+b"."+sig).decode()
ctx=ssl.create_default_context();ctx.check_hostname=False;ctx.verify_mode=ssl.CERT_NONE
def call(body):
    req=urllib.request.Request("https://localhost:8000/api/OGARecommendationHistoryReport",
        data=json.dumps(body).encode(),method="POST",
        headers={"Content-Type":"application/json","Authorization":"Bearer "+token})
    with urllib.request.urlopen(req,context=ctx,timeout=180) as r: return json.loads(r.read().decode())

j=call({"ReferenceNo":"001-005-090320211846","page":1,"pageSize":1000})
print("pageSize=1000 -> data rows:", len(j["data"]), "| totalCount:", j["totalCount"], "| isTotalCountExact:", j.get("isTotalCountExact"), "| totalPages:", j.get("totalPages"))
types={}
for row in j["data"]: types[row.get("type")]=types.get(row.get("type"),0)+1
print("data rows by type:", types)
