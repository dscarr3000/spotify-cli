using Newtonsoft.Json;
using SpotifyAPI.Web;
using SpotifyAPI.Web.Auth;
using static SpotifyAPI.Web.PlayerSetRepeatRequest;
using static SpotifyAPI.Web.Scopes;

namespace SpotifyCLI;

public class Program
{
    private const string CredentialsPath = "credentials.json";
    private static readonly string clientId = "5457209de7cf433299a3755e95d8ab47";
    private static readonly EmbedIOAuthServer _server = new(new Uri("http://localhost:3000/callback"), 3000);

    public static async Task<int> Main()
    {
        var spotify = await LoginAuthentication();

        Console.WriteLine($"Logged in as {(await spotify.UserProfile.Current()).DisplayName}.");

        var input = "";
        while (input != "q" && input != "quit")
        {
            input = Console.ReadLine();
            switch (input)
            {
                case "playlists":
                    await foreach (var playlist in spotify.Paginate(await spotify.Playlists.CurrentUsers()))
                        Console.WriteLine(playlist.Name);
                    break;
                case "q":
                case "quit":
                    break;
                default:
                    Console.WriteLine("Unknown Command");
                    break;
            }
        }

        //var devices = await spotify.Player.GetAvailableDevices();
        //foreach (var device in devices.Devices)
        //{
        //    Console.WriteLine(device.Name);
        //}
        ////var iphoneID = devices.Devices[0].Id;
        ////await spotify.Player.TransferPlayback(new PlayerTransferPlaybackRequest(new List<string> { iphoneID }));
        //await spotify.Player.PausePlayback();


        //await foreach (var playlist in spotify.Paginate(await spotify.Playlists.CurrentUsers()))
        //{
        //    Console.WriteLine(playlist.Name);
        //    var actualPlaylist = await spotify.Playlists.Get(playlist.Id);
        //    foreach (PlaylistTrack<IPlayableItem> item in actualPlaylist.Tracks!.Items!)
        //    {
        //        if (item.Track is FullTrack track)
        //        {
        //            Console.WriteLine($"\t{track.Name}");
        //        }
        //        else if (item.Track is FullEpisode episode)
        //        {
        //            Console.WriteLine($"\t{episode.Name}");
        //        }
        //    }
        //}

        //var me = await spotify.UserProfile.Current();
        //Console.WriteLine($"Welcome {me.DisplayName} ({me.Id}), you're authenticated!");

        //var playlists = await spotify.PaginateAll(await spotify.Playlists.CurrentUsers().ConfigureAwait(false));
        //Console.WriteLine($"Total Playlists in your Account: {playlists.Count}");

        return 0;
    }

    private static void ListPlaylists()
    {

    }

    private static async Task<SpotifyClient> LoginAuthentication()
    {
        if (File.Exists(CredentialsPath))
            return await RefreshAuthentication();
        return await NewAuthentication();
    }

    private static async Task<SpotifyClient> RefreshAuthentication()
    {
        var credentialsJSON = await File.ReadAllTextAsync(CredentialsPath);
        var token = JsonConvert.DeserializeObject<PKCETokenResponse>(credentialsJSON);
        var authenticator = new PKCEAuthenticator(clientId, token!);
        authenticator.TokenRefreshed += (_, token) => File.WriteAllText(CredentialsPath, JsonConvert.SerializeObject(token));
        var config = SpotifyClientConfig.CreateDefault().WithAuthenticator(authenticator);
        return new SpotifyClient(config);
    }

    private static async Task<SpotifyClient> NewAuthentication()
    {
        var (verifier, challenge) = PKCEUtil.GenerateCodes();
        await _server.Start();
        var tcs = new TaskCompletionSource<bool>();
        _server.AuthorizationCodeReceived += async (sender, response) =>
        {
            await _server.Stop();
            PKCETokenResponse token = await new OAuthClient().RequestToken(
                new PKCETokenRequest(clientId, response.Code, _server.BaseUri, verifier));
            await File.WriteAllTextAsync(CredentialsPath, JsonConvert.SerializeObject(token));
            tcs.TrySetResult(true);
        };

        var request = new LoginRequest(_server.BaseUri, clientId!, LoginRequest.ResponseType.Code)
        {
            CodeChallenge = challenge,
            CodeChallengeMethod = "S256",
            Scope = new List<string> { UserReadEmail, UserReadPrivate, PlaylistReadPrivate, PlaylistReadCollaborative,
                UserModifyPlaybackState, UserReadPlaybackState }
        };

        Uri uri = request.ToUri();
        try
        {
            BrowserUtil.Open(uri);
        }
        catch (Exception)
        {
            Console.WriteLine("Unable to open URL, manually open: {0}", uri);
        }

        await tcs.Task;
        return await RefreshAuthentication();
    }
}