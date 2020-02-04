# Traktor
Sync local library with Trakt.tv

<h3>Commandline parameters</h3>
<ul>
  <li>interval=&lt;TimeSpan&gt; (how often should Traktor query trakt for updates)</li>
  <li>--urls "binding" (Set a URL and port to bind web interface, if absent web interface is disabled.)
</ul>

<h3>appsettings.json (auto generated with default settings on first run)</h3>
<table>
    <thead>
        <tr>
            <th>Name</th>
            <th>Type</th>
            <th>Description</th>
            <th>Default</th>
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
            <th colspan="4">
                Configuration section for the built-in torrent client (MonoTorrent)
            </th>
        </tr>
        <tr>
            <td>Download.Path</td>
            <td>string</td>
            <td>Relative or full path to download location.</td>
            <td>"Downloads"</td>
        </tr>
        <tr>
            <td>Download.Port</td>
            <td>int</td>
            <td>Port used primarily for DHT connectivity?</td>
            <td>3333</td>
        </tr>
        <tr>
            <td>Download.MaximumDownloadSpeedKb</td>
            <td>int</td>
            <td>Maximum download speed in Kilobytes.</td>
            <td>5120</td>
        </tr>
        <tr>
            <td>Download.MaximumUploadSpeedKb</td>
            <td>int</td>
            <td>Maximum upload speed in Kilobytes.</td>
            <td>100</td>
        </tr>
        <tr>
            <td>Download.MaxConcurrent</td>
            <td>int</td>
            <td>Maximum concurrent downloads.</td>
            <td>3</td>
        </tr>
        <tr>
            <th colspan="4">
                Configuration section for filehandling, what to do with files after they're downloaded.
            </th>
        </tr>
        <tr>
            <td>File.MediaDestinations</td>
            <td>Dictionary(string, string)</td>
            <td>List of locations to relocate media types. (Movie and Episode)</td>
            <td>
                "Episode": "Episodes",<br />
                "Movie": "Movies"
            </td>
        </tr>
        <tr>
            <td>File.CleanUpSource</td>
            <td>bool</td>
            <td>Delete any leftover files and folders in the download location after moving media.</td>
            <td>true</td>
        </tr>
        <tr>
            <td>File.IncludeSubs</td>
            <td>bool</td>
            <td>In addition to media, also move any .sub files.</td>
            <td>true</td>
        </tr>
        <tr>
            <td>File.MediaTypes</td>
            <td>string[]</td>
            <td>File types to consider as media files and move to <b>MediaDestinations</b></td>
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
        <tr>
            <th colspan="4">
                <p>Configuration section for scouting media on indexers.</p>
                <p>Scout.Requirements is a list of requirement objects detailing how to search for torrents and what parameters they must meet to be accepted as valid sources.</p>
            </th>
        </tr>
        <tr>
            <td>Scout.Requirements[].MediaType</td>
            <td>string</td>
            <td>What type of media this requirement is for. (Movie/Episode)</td>
            <td>"Episode"/"Movie"</td>
        </tr>
        <tr>
            <td>Scout.Requirements[].ReleaseDateDeadlineTime</td>
            <td>TimeSpan</td>
            <td>On release day, never wait longer than this time, overrules patience setting but only on release date.</td>
            <td>null/null</td>
        </tr>
        <tr>
            <td>Scout.Requirements[].Delay</td>
            <td>TimeSpan</td>
            <td>Minimum time to wait from release before delivering candidates.</td>
            <td>null/"1.00:00:00"</td>
        </tr>
        <tr>
            <td>Scout.Requirements[].Timeout</td>
            <td>TimeSpan</td>
            <td>Maximum time to wait from release for suitable candidates before abandoning item.</td>
            <td>"7.00:00:00"/null</td>
        </tr>
        <tr>
            <td>Scout.Requirements[].NoResultThrottle</td>
            <td>TimeSpan</td>
            <td>Time to wait before scouting media again after finding no results.</td>
            <td>null/"1.00:00:00"</td>
        </tr>
        <tr>
            <td>Scout.Requirements[].Parameters</td>
            <td>Array</td>
            <td>
                <p>Parameters torrent must met</p>
            </td>
            <td>LUL</td>
        </tr>
    </tbody>
</table>

<h4>Parameter objects</h4>
<table>
    <thead>
        <tr>
            <th>Name</th>
            <th>Type</th>
            <th>Description</th>
        </tr>
    </thead>
    <tbody>
        <tr>
            <td>Category</td>
            <td>string</td>
            <td>
                <ul>
                    <li>Resolution</li>
                    <li>Audio</li>
                    <li>Source</li>
                    <li>Tag</li>
                    <li>Group</li>
                    <li>SizeMb</li>
                    <li>FreeText</li>
                </ul>
            </td>
        </tr>
        <tr>
            <td>Comparison</td>
            <td>string</td>
            <td>
                <ul>
                    <li>Equal</li>
                    <li>NotEqual</li>
                    <li>Minimum</li>
                    <li>Maximum</li>
                    <li>Group</li>
                    <li>SizeMb</li>
                    <li>FreeText</li>
                </ul>
            </td>
        </tr>
        <tr>
            <td>Definition</td>
            <td>string[]</td>
            <td>
                <p>If Category is <b>Resolution</b></p>
                <ul>
                    <li>HD_720p</li>
                    <li>FHD_1080p</li>
                    <li>UHD_2160p</li>
                </ul>
                <p>
                    If Category is <b>Audio/Source/Tag</b>
                </p>
                <ul>
                    <li>AAC</li>
                    <li>DTS</li>
                    <li>DTS_HD</li>
                    <li>DTS_HD_MA</li>
                    <li>AC7_1</li>
                    <li>AC5_1</li>
                    <li>Atmos</li>
                    <li>WEB_DL</li>
                    <li>BluRay</li>
                    <li>PROPER</li>
                    <li>REPACK</li>
                </ul>
                <p>If Category is <b>Group/FreeText</b> then name of a release group or whatever you want to be in the name of the torrent.</p>
                <p>If Category is <b>SizeMb</b> then a size in megabytes.</p>
            </td>
        </tr>
        <tr>
            <td>Patience</td>
            <td>TimeSpan</td>
            <td>
                How long the scouter will wait for a source that meets this parameter, set to "00:00:00" to make it a preference only or null to make it a hard requirement.
            </td>
        </tr>
        <tr>
            <td>Weight</td>
            <td>int</td>
            <td>
                Set how heavy this parameter should be weighted when comparing potential sources.
            </td>
        </tr>
    </tbody>
</table>

<h3>StartTraktorWithVPN.bat<h3>
  <pre>cls
rasdial|find /I "ExpressVPN"

if %errorlevel% neq 0 (rasdial "&lt;VPN name&gt;" &lt;user&gt; &lt;pass&gt;)
if %errorlevel% neq 0 (rasdial "&lt;VPN name (secondary)&gt;" &lt;user&gt; &lt;pass&gt;)
Traktor.exe loglevel=Debug --urls "http://*:5000" --environment "Development"
if %errorlevel% neq 0 exit /b %errorlevel%</pre>
