using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace DotNetCloud.Modules.Music.Host.Subsonic;

/// <summary>
/// Subsonic API response wrapper. Supports both XML and JSON serialization.
/// </summary>
[XmlRoot("subsonic-response", Namespace = "http://subsonic.org/restapi")]
public sealed class SubsonicResponse
{
    /// <summary>API response status.</summary>
    [XmlAttribute("status")]
    [JsonPropertyName("status")]
    public string Status { get; set; } = "ok";

    /// <summary>Subsonic API version.</summary>
    [XmlAttribute("version")]
    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.16.1";

    /// <summary>Server type.</summary>
    [XmlAttribute("type")]
    [JsonPropertyName("type")]
    public string Type { get; set; } = "dotnetcloud";

    /// <summary>Server version.</summary>
    [XmlAttribute("serverVersion")]
    [JsonPropertyName("serverVersion")]
    public string ServerVersion { get; set; } = "0.1.0";

    /// <summary>Error details when status is "failed".</summary>
    [XmlElement("error")]
    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SubsonicError? Error { get; set; }

    /// <summary>Artists index result.</summary>
    [XmlElement("artists")]
    [JsonPropertyName("artists")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SubsonicArtistsResult? Artists { get; set; }

    /// <summary>Single artist result.</summary>
    [XmlElement("artist")]
    [JsonPropertyName("artist")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SubsonicArtistDetail? Artist { get; set; }

    /// <summary>Single album result.</summary>
    [XmlElement("album")]
    [JsonPropertyName("album")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SubsonicAlbum? Album { get; set; }

    /// <summary>Album list result.</summary>
    [XmlElement("albumList2")]
    [JsonPropertyName("albumList2")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SubsonicAlbumList? AlbumList2 { get; set; }

    /// <summary>Song result.</summary>
    [XmlElement("song")]
    [JsonPropertyName("song")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SubsonicSong? Song { get; set; }

    /// <summary>Random songs result.</summary>
    [XmlElement("randomSongs")]
    [JsonPropertyName("randomSongs")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SubsonicSongList? RandomSongs { get; set; }

    /// <summary>Search result.</summary>
    [XmlElement("searchResult3")]
    [JsonPropertyName("searchResult3")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SubsonicSearchResult? SearchResult3 { get; set; }

    /// <summary>Starred items result.</summary>
    [XmlElement("starred2")]
    [JsonPropertyName("starred2")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SubsonicStarred? Starred2 { get; set; }

    /// <summary>Playlists result.</summary>
    [XmlElement("playlists")]
    [JsonPropertyName("playlists")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SubsonicPlaylists? Playlists { get; set; }

    /// <summary>Playlist result.</summary>
    [XmlElement("playlist")]
    [JsonPropertyName("playlist")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SubsonicPlaylistDetail? Playlist { get; set; }

    /// <summary>Genres result.</summary>
    [XmlElement("genres")]
    [JsonPropertyName("genres")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SubsonicGenres? Genres { get; set; }

    /// <summary>License result for getLicense.</summary>
    [XmlElement("license")]
    [JsonPropertyName("license")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SubsonicLicense? License { get; set; }

    /// <summary>OpenSubsonic extensions.</summary>
    [XmlElement("openSubsonicExtensions")]
    [JsonPropertyName("openSubsonicExtensions")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SubsonicExtensions? OpenSubsonicExtensions { get; set; }

    /// <summary>Creates a failed response with an error.</summary>
    public static SubsonicResponse Failed(int code, string message) => new()
    {
        Status = "failed",
        Error = new SubsonicError { Code = code, Message = message }
    };

    /// <summary>Creates a successful empty response.</summary>
    public static SubsonicResponse Ok() => new();
}

/// <summary>Subsonic error element.</summary>
public sealed class SubsonicError
{
    /// <summary>Subsonic error code.</summary>
    [XmlAttribute("code")]
    [JsonPropertyName("code")]
    public int Code { get; set; }

    /// <summary>Human-readable error message.</summary>
    [XmlAttribute("message")]
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}

/// <summary>Artists index result.</summary>
public sealed class SubsonicArtistsResult
{
    /// <summary>Artist index entries.</summary>
    [XmlElement("index")]
    [JsonPropertyName("index")]
    public List<SubsonicArtistIndex> Index { get; set; } = [];
}

/// <summary>Artist index grouped by first letter.</summary>
public sealed class SubsonicArtistIndex
{
    /// <summary>The index letter.</summary>
    [XmlAttribute("name")]
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Artists in this index.</summary>
    [XmlElement("artist")]
    [JsonPropertyName("artist")]
    public List<SubsonicArtistSummary> Artist { get; set; } = [];
}

/// <summary>Summary artist entry.</summary>
public sealed class SubsonicArtistSummary
{
    /// <summary>Artist ID.</summary>
    [XmlAttribute("id")]
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>Artist name.</summary>
    [XmlAttribute("name")]
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Number of albums.</summary>
    [XmlAttribute("albumCount")]
    [JsonPropertyName("albumCount")]
    public int AlbumCount { get; set; }

    /// <summary>Whether this artist is starred.</summary>
    [XmlAttribute("starred")]
    [JsonPropertyName("starred")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Starred { get; set; }
}

/// <summary>Artist detail with albums.</summary>
public sealed class SubsonicArtistDetail
{
    /// <summary>Artist ID.</summary>
    [XmlAttribute("id")]
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>Artist name.</summary>
    [XmlAttribute("name")]
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Number of albums.</summary>
    [XmlAttribute("albumCount")]
    [JsonPropertyName("albumCount")]
    public int AlbumCount { get; set; }

    /// <summary>Albums by this artist.</summary>
    [XmlElement("album")]
    [JsonPropertyName("album")]
    public List<SubsonicAlbum> Album { get; set; } = [];
}

/// <summary>Subsonic album element.</summary>
public sealed class SubsonicAlbum
{
    /// <summary>Album ID.</summary>
    [XmlAttribute("id")]
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>Album name.</summary>
    [XmlAttribute("name")]
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Artist name.</summary>
    [XmlAttribute("artist")]
    [JsonPropertyName("artist")]
    public string Artist { get; set; } = string.Empty;

    /// <summary>Artist ID.</summary>
    [XmlAttribute("artistId")]
    [JsonPropertyName("artistId")]
    public string ArtistId { get; set; } = string.Empty;

    /// <summary>Number of songs.</summary>
    [XmlAttribute("songCount")]
    [JsonPropertyName("songCount")]
    public int SongCount { get; set; }

    /// <summary>Total duration in seconds.</summary>
    [XmlAttribute("duration")]
    [JsonPropertyName("duration")]
    public int Duration { get; set; }

    /// <summary>Cover art ID.</summary>
    [XmlAttribute("coverArt")]
    [JsonPropertyName("coverArt")]
    public string? CoverArt { get; set; }

    /// <summary>Release year.</summary>
    [XmlAttribute("year")]
    [JsonPropertyName("year")]
    public int Year { get; set; }

    /// <summary>Genre name.</summary>
    [XmlAttribute("genre")]
    [JsonPropertyName("genre")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Genre { get; set; }

    /// <summary>Whether starred.</summary>
    [XmlAttribute("starred")]
    [JsonPropertyName("starred")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Starred { get; set; }

    /// <summary>Songs on this album (only when getting album detail).</summary>
    [XmlElement("song")]
    [JsonPropertyName("song")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<SubsonicSong>? Song { get; set; }
}

/// <summary>Subsonic song/track element.</summary>
public sealed class SubsonicSong
{
    /// <summary>Track ID.</summary>
    [XmlAttribute("id")]
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>Track title.</summary>
    [XmlAttribute("title")]
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>Album name.</summary>
    [XmlAttribute("album")]
    [JsonPropertyName("album")]
    public string? Album { get; set; }

    /// <summary>Artist name.</summary>
    [XmlAttribute("artist")]
    [JsonPropertyName("artist")]
    public string Artist { get; set; } = string.Empty;

    /// <summary>Track number.</summary>
    [XmlAttribute("track")]
    [JsonPropertyName("track")]
    public int Track { get; set; }

    /// <summary>Disc number.</summary>
    [XmlAttribute("discNumber")]
    [JsonPropertyName("discNumber")]
    public int DiscNumber { get; set; }

    /// <summary>Release year.</summary>
    [XmlAttribute("year")]
    [JsonPropertyName("year")]
    public int Year { get; set; }

    /// <summary>Genre name.</summary>
    [XmlAttribute("genre")]
    [JsonPropertyName("genre")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Genre { get; set; }

    /// <summary>Duration in seconds.</summary>
    [XmlAttribute("duration")]
    [JsonPropertyName("duration")]
    public int Duration { get; set; }

    /// <summary>File size in bytes.</summary>
    [XmlAttribute("size")]
    [JsonPropertyName("size")]
    public long Size { get; set; }

    /// <summary>Content type.</summary>
    [XmlAttribute("contentType")]
    [JsonPropertyName("contentType")]
    public string ContentType { get; set; } = string.Empty;

    /// <summary>File suffix.</summary>
    [XmlAttribute("suffix")]
    [JsonPropertyName("suffix")]
    public string Suffix { get; set; } = string.Empty;

    /// <summary>Bitrate in kbps.</summary>
    [XmlAttribute("bitRate")]
    [JsonPropertyName("bitRate")]
    public int BitRate { get; set; }

    /// <summary>Album ID.</summary>
    [XmlAttribute("albumId")]
    [JsonPropertyName("albumId")]
    public string? AlbumId { get; set; }

    /// <summary>Artist ID.</summary>
    [XmlAttribute("artistId")]
    [JsonPropertyName("artistId")]
    public string ArtistId { get; set; } = string.Empty;

    /// <summary>Cover art ID.</summary>
    [XmlAttribute("coverArt")]
    [JsonPropertyName("coverArt")]
    public string? CoverArt { get; set; }

    /// <summary>Whether starred.</summary>
    [XmlAttribute("starred")]
    [JsonPropertyName("starred")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Starred { get; set; }
}

/// <summary>Song list wrapper.</summary>
public sealed class SubsonicSongList
{
    /// <summary>Songs in the list.</summary>
    [XmlElement("song")]
    [JsonPropertyName("song")]
    public List<SubsonicSong> Song { get; set; } = [];
}

/// <summary>Album list wrapper.</summary>
public sealed class SubsonicAlbumList
{
    /// <summary>Albums in the list.</summary>
    [XmlElement("album")]
    [JsonPropertyName("album")]
    public List<SubsonicAlbum> Album { get; set; } = [];
}

/// <summary>Search result.</summary>
public sealed class SubsonicSearchResult
{
    /// <summary>Matching artists.</summary>
    [XmlElement("artist")]
    [JsonPropertyName("artist")]
    public List<SubsonicArtistSummary> Artist { get; set; } = [];

    /// <summary>Matching albums.</summary>
    [XmlElement("album")]
    [JsonPropertyName("album")]
    public List<SubsonicAlbum> Album { get; set; } = [];

    /// <summary>Matching songs.</summary>
    [XmlElement("song")]
    [JsonPropertyName("song")]
    public List<SubsonicSong> Song { get; set; } = [];
}

/// <summary>Starred items result.</summary>
public sealed class SubsonicStarred
{
    /// <summary>Starred artists.</summary>
    [XmlElement("artist")]
    [JsonPropertyName("artist")]
    public List<SubsonicArtistSummary> Artist { get; set; } = [];

    /// <summary>Starred albums.</summary>
    [XmlElement("album")]
    [JsonPropertyName("album")]
    public List<SubsonicAlbum> Album { get; set; } = [];

    /// <summary>Starred songs.</summary>
    [XmlElement("song")]
    [JsonPropertyName("song")]
    public List<SubsonicSong> Song { get; set; } = [];
}

/// <summary>Playlists result.</summary>
public sealed class SubsonicPlaylists
{
    /// <summary>Playlist entries.</summary>
    [XmlElement("playlist")]
    [JsonPropertyName("playlist")]
    public List<SubsonicPlaylistSummary> Playlist { get; set; } = [];
}

/// <summary>Playlist summary.</summary>
public class SubsonicPlaylistSummary
{
    /// <summary>Playlist ID.</summary>
    [XmlAttribute("id")]
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>Playlist name.</summary>
    [XmlAttribute("name")]
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Song count.</summary>
    [XmlAttribute("songCount")]
    [JsonPropertyName("songCount")]
    public int SongCount { get; set; }

    /// <summary>Total duration in seconds.</summary>
    [XmlAttribute("duration")]
    [JsonPropertyName("duration")]
    public int Duration { get; set; }

    /// <summary>Whether public.</summary>
    [XmlAttribute("public")]
    [JsonPropertyName("public")]
    public bool IsPublic { get; set; }

    /// <summary>Owner username.</summary>
    [XmlAttribute("owner")]
    [JsonPropertyName("owner")]
    public string Owner { get; set; } = string.Empty;
}

/// <summary>Playlist detail with entries.</summary>
public sealed class SubsonicPlaylistDetail : SubsonicPlaylistSummary
{
    /// <summary>Songs in the playlist.</summary>
    [XmlElement("entry")]
    [JsonPropertyName("entry")]
    public List<SubsonicSong> Entry { get; set; } = [];
}

/// <summary>Genres result.</summary>
public sealed class SubsonicGenres
{
    /// <summary>Genre entries.</summary>
    [XmlElement("genre")]
    [JsonPropertyName("genre")]
    public List<SubsonicGenre> Genre { get; set; } = [];
}

/// <summary>Genre entry.</summary>
public sealed class SubsonicGenre
{
    /// <summary>Genre name (stored as element text).</summary>
    [XmlText]
    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;

    /// <summary>Song count.</summary>
    [XmlAttribute("songCount")]
    [JsonPropertyName("songCount")]
    public int SongCount { get; set; }

    /// <summary>Album count.</summary>
    [XmlAttribute("albumCount")]
    [JsonPropertyName("albumCount")]
    public int AlbumCount { get; set; }
}

/// <summary>License info for getLicense.</summary>
public sealed class SubsonicLicense
{
    /// <summary>Whether licensed (always true for open-source).</summary>
    [XmlAttribute("valid")]
    [JsonPropertyName("valid")]
    public bool Valid { get; set; } = true;

    /// <summary>Email.</summary>
    [XmlAttribute("email")]
    [JsonPropertyName("email")]
    public string Email { get; set; } = "admin@localhost";

    /// <summary>License expiry date.</summary>
    [XmlAttribute("licenseExpires")]
    [JsonPropertyName("licenseExpires")]
    public string LicenseExpires { get; set; } = "2099-12-31T00:00:00";
}

/// <summary>OpenSubsonic extensions list.</summary>
public sealed class SubsonicExtensions
{
    /// <summary>List of supported extensions.</summary>
    [XmlElement("openSubsonicExtension")]
    [JsonPropertyName("openSubsonicExtension")]
    public List<SubsonicExtension> OpenSubsonicExtension { get; set; } = [];
}

/// <summary>Single OpenSubsonic extension.</summary>
public sealed class SubsonicExtension
{
    /// <summary>Extension name.</summary>
    [XmlAttribute("name")]
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Supported versions of this extension.</summary>
    [XmlAttribute("versions")]
    [JsonPropertyName("versions")]
    public string Versions { get; set; } = string.Empty;
}
