# SOOP provider notes

HIKI Notifier 1.15c accepts a SOOP channel identifier or a channel URL on
play.sooplive.co.kr, play.sooplive.com, or legacy play.afreecatv.com.
It stores the canonical form https://play.sooplive.co.kr/{channel}.

The public HTML5 player live endpoint is the live source. RESULT=1 with a
broadcast number (BNO) is Live. RESULT=0 is Offline. HTTP, timeout, JSON,
and unexpected-schema failures are Unknown. BNO suppresses duplicate alerts.

VODs, clips, chat, comments, statistics, login, cookies, and browser automation
are outside this provider.
