from characters import build_cookie
from acio import save_acm, stats
m, info = build_cookie(); save_acm(m, "models/char_cookie.acm"); print(stats(m))
