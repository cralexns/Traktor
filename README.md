# Traktor
Sync local library with Trakt.tv

<h3>StartTraktorWithVPN.bat
  <pre>cls
rasdial|find /I "ExpressVPN"

if %errorlevel% neq 0 (rasdial "&gt;VPN name&gt;" &lt;user&gt; &lt;pass&gt;)
if %errorlevel% neq 0 (rasdial "&gt; secondary VPN name&gt;" &lt;user&gt; &lt;pass&gt;)
Traktor.exe loglevel=Debug --urls "http://*:5000" --environment "Development"
if %errorlevel% neq 0 exit /b %errorlevel%</pre>
