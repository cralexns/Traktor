# Traktor
Sync local library with Trakt.tv

<h3>StartTraktorWithVPN.bat
  <pre>cls
rasdial|find /I "ExpressVPN"

if %errorlevel% neq 0 (rasdial "ExpressVPN (SE)" <user> <pass>)
if %errorlevel% neq 0 (rasdial "ExpressVPN (NL)" <user> <pass>)
Traktor.exe loglevel=Debug --urls "http://*:5000" --environment "Development"
if %errorlevel% neq 0 exit /b %errorlevel%</pre>
