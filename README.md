# Traktor
Sync local library with Trakt.tv

<h3>StartTraktorWithVPN.bat<h3>
  <pre>cls
rasdial|find /I "ExpressVPN"

if %errorlevel% neq 0 (rasdial "&lt;VPN name&gt;" &lt;user&gt; &lt;pass&gt;)
if %errorlevel% neq 0 (rasdial "&lt;VPN name (secondary)&gt;" &lt;user&gt; &lt;pass&gt;)
Traktor.exe loglevel=Debug --urls "http://*:5000" --environment "Development"
if %errorlevel% neq 0 exit /b %errorlevel%</pre>

<h3>appsettings.json (auto generated with default settings on first run)</h3>
<table>
    <thead>
        <tr>
            <td>Name</td>
            <td>Type</td>
            <td>Description</td>
            <td>Default</td>
        </tr>
    </thead>
    <tbody>
        <tr>
            <td>SynchronizeCollection</td>
            <td>bool</td>
            <td>If media is removed from collection on trakt, remove it locally. (Only removes media originally fetched by Traktor)</td>
            <td>true</td>
        </tr>
        <tr>
            <td>ScoutFrequency</td>
            <td>TimeSpan</td>
            <td>How often to scout media on indexers.</td>
            <td>"00:30:00"</td>
        </tr>
        <tr>
            <td>MaximumCalendarLookbackDays</td>
            <td>int</td>
            <td>The maximum amount of days in the past Traktor is allowed to query the calendar. (Only matters on first run or if script is run very irregularly)</td>
            <td>30</td>
        </tr>
        <tr>
            <td>IgnoreSpecialSeasons</td>
            <td>bool</td>
            <td>Ignore special seasons on shows (0), special episodes are usually categorized as season 0.</td>
            <td>true</td>
        </tr>
        <tr>
            <td>ExcludeUnwatchedShowsFromCalendar</td>
            <td>bool</td>
            <td>If you've collected a single episode of a show on trakt, it will appear on your calendar - with this setting enabled shows will be ignored unless you've watched at least one episode.</td>
            <td>true</td>
        </tr>
        <tr>
            <td>EnsureDownloadIntegrity</td>
            <td>bool</td>
            <td>??</td>
            <td>false</td>
        </tr>
        <tr>
            <td>FetchImages</td>
            <td>string</td>
            <td>
                Fetch posters for movies and episodes for display in the web interface. Possible values: Never, ExcludeCollection, All
            </td>
            <td>"ExcludeCollection"</td>
        </tr>
        <tr>
            <td>RenameFilePattern</td>
            <td>Dictionary(string, string)</td>
            <td>Rename files according to trakt data and the specified format. (Movie and Episode)</td>
            <td>"Episode": "{ShowTitle} - {Season}x{Number:00} - {Title}"</td>
        </tr>
        <tr>
            <td>Download</td>
            <td></td>
            <td>
                <p>Configuration section for the built-in torrent client (MonoTorrent)</p>
                <table>
                    <thead>
                        <tr>
                            <td>Name</td>
                            <td>Type</td>
                            <td>Description</td>
                            <td>Default</td>
                        </tr>
                    </thead>
                    <tbody>
                        <tr>
                            <td>Path</td>
                            <td>string</td>
                            <td>Relative or full path to download location.</td>
                            <td>"Downloads"</td>
                        </tr>
                        <tr>
                            <td>Port</td>
                            <td>int</td>
                            <td>Port used primarily for DHT connectivity?</td>
                            <td>3333</td>
                        </tr>
                        <tr>
                            <td>MaximumDownloadSpeedKb</td>
                            <td>int</td>
                            <td>Maximum download speed in Kilobytes.</td>
                            <td>5120</td>
                        </tr>
                        <tr>
                            <td>MaximumUploadSpeedKb</td>
                            <td>int</td>
                            <td>Maximum upload speed in Kilobytes.</td>
                            <td>100</td>
                        </tr>
                        <tr>
                            <td>MaxConcurrent</td>
                            <td>int</td>
                            <td>Maximum concurrent downloads.</td>
                            <td>3</td>
                        </tr>
                    </tbody>
                </table>
            </td>
            <td>-</td>
        </tr>
        <tr>
            <td>File</td>
            <td></td>
            <td>
                <p>Configuration section for filehandling, what to do with files after they're downloaded.</p>
                <table>
                    <thead>
                        <tr>
                            <td>Name</td>
                            <td>Type</td>
                            <td>Description</td>
                            <td>Default</td>
                        </tr>
                    </thead>
                    <tbody>
                        <tr>
                            <td>MediaDestinations</td>
                            <td>Dictionary(string, string)</td>
                            <td>List of locations to relocate media types. (Movie and Episode)</td>
                            <td>
                                "Episode": "Episodes",<br />
                                "Movie": "Movies"
                            </td>
                        </tr>
                        <tr>
                            <td>CleanUpSource</td>
                            <td>bool</td>
                            <td>Delete any leftover files and folders in the download location after moving media.</td>
                            <td>true</td>
                        </tr>
                        <tr>
                            <td>IncludeSubs</td>
                            <td>bool</td>
                            <td>In addition to media, also move any .sub files.</td>
                            <td>true</td>
                        </tr>
                        <tr>
                            <td>MediaTypes</td>
                            <td>string[]</td>
                            <td>File types to consider as media files.</td>
                            <td>
                                "mkv",<br />
                                "mp4",<br />
                                "mpeg",<br />
                                "avi",<br />
                                "wmv",<br />
                                "rm",<br />
                                "divx",<br />
                                "webm"
                            </td>
                        </tr>
                    </tbody>
                </table>
            </td>
            <td>-</td>
        </tr>
    </tbody>
</table>
