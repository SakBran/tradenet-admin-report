Subject: [Urgent] reportapi.myanmartradenet.com — origin ၂ ခု အလှည့်ကျ ဖြေဆိုနေပါသည် (CORS / 404 ပြဿနာ)

မင်္ဂလာပါ,

report.myanmartradenet.com (Tradenet Admin Report) မှာ sign-in လုပ်လို့ မရဘဲ browser ထဲမှာ
CORS error တင်နေတဲ့ ပြဿနာကို စစ်ဆေးပြီးပါပြီ။ Application (API) ဘက် code ပြဿနာ မဟုတ်ပါ။
`reportapi.myanmartradenet.com` ကို Cloudflare နောက်ကွယ်မှာ **origin ၂ ခု** က
တစ်ခုကျော်တစ်ခု အလှည့်ကျ ဖြေဆိုနေတာ ဖြစ်ပါတယ်။ တစ်ခုက app တင်ထားတဲ့ server (ကောင်း)၊
နောက်တစ်ခုက **app မတင်ထားတဲ့ IIS server** (မကောင်း) ဖြစ်ပါတယ်။

စစ်ဆေးချိန်: 2026-08-27, 05:30–05:45 UTC (public internet မှ)

---

## ၁။ အဓိက အထောက်အထား — တူတူ request ကို ၁၂ ခါ ပို့ကြည့်ခြင်း

GET https://reportapi.myanmartradenet.com/health x12
→ 6 x 200 (body 2 bytes = "ok") ← app server (ကောင်း)
→ 6 x 404 (body 2093 bytes = IIS error page) ← app မရှိတဲ့ server

OPTIONS https://reportapi.myanmartradenet.com/api/Auth x12
(Origin: https://report.myanmartradenet.com, Access-Control-Request-Method: POST)
→ 6 x 204 + CORS header အားလုံးပါ ← app server (ကောင်း)
→ 6 x 200 + CORS header တစ်လုံးမှ မပါ ← app မရှိတဲ့ server ❌

POST https://reportapi.myanmartradenet.com/api/Auth x10 (sign-in လုပ်တဲ့ request)
→ 5 x 400 (254 bytes, application/problem+json) ← app server
→ 5 x 404 (2093 bytes, IIS error page) ← app မရှိတဲ့ server

GET https://reportapi.myanmartradenet.com/ x10
→ 5 x 200 (1553 bytes = IIS ရဲ့ "IIS Windows Server" default page)
→ 5 x 404 (0 bytes = app ရဲ့ 404)

အလှည့်ကျပုံက အတိအကျ 1:1 (200,404,200,404,…) ဖြစ်ပါတယ်။

---

## ၂။ Browser က CORS error ပြရတဲ့ တိုက်ရိုက်အကြောင်းရင်း

app မရှိတဲ့ server က CORS preflight (OPTIONS) ကို IIS ရဲ့ OPTIONSVerbHandler နဲ့ ဖြေပါတယ်:

    HTTP/2 200
    content-length: 0
    allow:  OPTIONS, TRACE, GET, HEAD, POST
    public: OPTIONS, TRACE, GET, HEAD, POST      ← IIS OPTIONSVerbHandler ရဲ့ လက်မှတ်
    cf-cache-status: DYNAMIC
    (access-control-allow-origin  မပါ)
    cf-ray: a318bb5eef8c97d6-FRA                ← Cloudflare log ထဲ ရှာနိုင်ပါတယ်

`Public:` + `Allow: OPTIONS, TRACE, GET, HEAD, POST` header ဆိုတာ
"IIS ကိုယ်တိုင် ဖြေလိုက်တာ၊ application ဆီ တစ်ခါမှ မရောက်ဘူး" ဆိုတဲ့ အတိအကျ လက်မှတ်ပါ။
`Access-Control-Allow-Origin` မပါတဲ့အတွက် browser က block လုပ်ပြီး CORS error ပြပါတယ်။

နှိုင်းယှဉ်ချက် — app server (ကောင်းတဲ့တစ်ခု) ရဲ့ တူတူ request ရဲ့ အဖြေ:

    HTTP/2 204
    vary: Origin
    access-control-allow-origin: https://report.myanmartradenet.com
    access-control-allow-credentials: true
    access-control-allow-headers: content-type
    access-control-allow-methods: POST
    access-control-max-age: 3600
    cf-ray: a318bb680c1ad2cf-FRA

ဆိုတော့ API ရဲ့ CORS configuration က မှန်ပါတယ်။ ပြဿနာက ဘယ် server ဆီ ရောက်သွားလဲ ဆိုတာပါ။

သက်ရောက်မှု: preflight က 50% fail၊ preflight အောင်ရင်တောင် ပြီးခါမှ POST က 50% သာ
app ဆီရောက်တာမို့ — user တစ်ဦး sign-in အောင်နိုင်ခြေ ~25% သာ ဖြစ်ပါတယ်။

---

## ၃။ ပြဿနာက Cloudflare edge မဟုတ်၊ origin ဘက် ဖြစ်ပါတယ်

DNS (နှစ်ခုစလုံး Cloudflare proxy အောက်):
report.myanmartradenet.com → 104.21.36.128 , 172.67.194.94
reportapi.myanmartradenet.com → 172.67.194.94 , 104.21.36.128

Cloudflare edge IP တစ်ခုချင်း pin လုပ်ပြီး စစ်ကြည့်တဲ့အခါ ၂ ခုစလုံးမှာ အလှည့်ကျပါတယ်:

    curl --resolve reportapi.myanmartradenet.com:443:104.21.36.128 .../health
        → 200 404 200 404 200 404 200 404
    curl --resolve reportapi.myanmartradenet.com:443:172.67.194.94 .../health
        → 200 404 200 404 200 404 200 404

edge IP နှစ်လုံးစလုံးမှာ တူတူ ဖြစ်နေတာမို့ ခွဲသွားတာက **Cloudflare ရဲ့ နောက်ကွယ်
(origin ရွေးချယ်တဲ့ အဆင့်)** မှာ ဖြစ်ပါတယ်။ Cache လည်း မဟုတ်ပါ — `?cb=9` လို
cache-busting query ထည့်လည် အတူတူပါ (`cf-cache-status: DYNAMIC`)။

မကောင်းတဲ့ server ရဲ့ အမှတ်အသား: `/` မှာ IIS ရဲ့ စက်ရုံထွက် "IIS Windows Server"
welcome page (1553 bytes, အပြာနောက်ခံ) ပြန်ပါတယ် ⇒ IIS run နေတယ်၊ ဒါပေမဲ့
**ဒီ host header အတွက် site/application binding မရှိပါ** (ဒါမှမဟုတ် app မတင်ရသေးပါ)။

---

## ၄။ ကျေးဇူးပြုပြီး စစ်ပေးရန် (ဖြစ်နိုင်ခြေ အစဉ်လိုက်)

(၁) Cloudflare Zero Trust → Networks → Tunnels
`reportapi.myanmartradenet.com` ကို ဖြေဆိုနေတဲ့ tunnel မှာ **connector ၂ ခု**
ရှိမရှိ။ (cloudflared က server အသစ်+အိုနှစ်လုံးမှာ service အနေနဲ့ run နေတာ
ဖြစ်နိုင်ပါတယ်။) ဒါမှမဟုတ် ဒီ hostname ကို tunnel ၂ ခုက publish လုပ်နေတာ။
→ အသုံးမဝင်တဲ့/အိုတဲ့ connector ကို ရပ်ပေးပါ။

(၂) Cloudflare DNS
`reportapi` record အောက်မှာ tunnel CNAME အပြင် **A record အပို** ကျန်နေမလား။
→ ကျန်နေရင် ဖျက်ပေးပါ။

(၃) Cloudflare Load Balancer သုံးထားရင်
pool ထဲမှာ deploy မရောက်တဲ့ origin ပါနေမလား → ထုတ်ပေးပါ
(deploy က share တစ်ခု `M:\T20-ADMIN-REPORT-BACKEND` ကိုပဲ copy ပါတယ်)။

(၄) cloudflared config ရဲ့ `service:` target hostname က
internal DNS မှာ IP ၂ လုံး resolve ဖြစ်နေမလား။

မှတ်ချက်: မကောင်းတဲ့ server ပေါ်မှာ CORS setting ထည့်တာ အဖြေ မဟုတ်ပါ —
အဲ့ဒီ server မှာ application ကိုယ်တိုင် မရှိပါ။ Rotation ထဲက ထုတ်တာ (ဒါမှမဟုတ်
အဲ့ဒီ server ပေါ် app + host header binding အပြည့်အစုံ တင်တာ) သာ အဖြေ ဖြစ်ပါတယ်။

---

## ၅။ ပြင်ပြီးရင် အတည်ပြုနည်း

    for i in $(seq 1 10); do \
      curl -s -m 15 -o /dev/null -w "%{http_code} %{size_download}\n" \
        https://reportapi.myanmartradenet.com/health; \
    done | sort | uniq -c

ရလာဒ် ကောင်းရင် → 10 x "200 2" (တစ်ခုမှ 404 မပါရ)
404 / 2093 bytes ပါနေရင် → မကောင်းတဲ့ origin က rotation ထဲ ရှိနေဆဲ ဖြစ်ပါတယ်။

ကျေးဇူးတင်ပါသည်။
