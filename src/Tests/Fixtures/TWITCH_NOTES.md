# Twitch fixtures

Captured from unauthenticated HTTP GETs of https://www.twitch.tv/eslcs and https://www.twitch.tv/twitch on 2026-09-24 around 16:50 UTC.
Only the ProfilePage and top-level broadcast VideoObject JSON-LD nodes are retained. Scripts, artwork, cookies and VOD lists are omitted.
Tests use a fixed clock so the LIVE fixture's SEO endDate does not expire during regression runs.
The profile-only fixture establishes NotLive (displayed as Offline) under the app's state policy: a normally parsed, verified channel with no current broadcast.
The eslcs-notlive fixture is derived from the captured eslcs page by retaining its ProfilePage and removing its broadcast. It is synthetic, not a separately captured offline observation.
Transition checks combine synthetic status sequences and the HTTP/HTML parsing pipeline. Structure failures and contradictory live metadata must remain Unknown.
