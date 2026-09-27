using Windows.Media;
using Windows.Media.Control;

namespace PixelPro2;

public sealed class MusicPlugin : IDisposable {
    GlobalSystemMediaTransportControlsSessionManager? manager;
    bool failed;

    public async Task<NowPlayingSnapshot?> Read() {
        if(failed)return null;
        try {
            manager ??= await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            var session=manager.GetCurrentSession();
            if(session==null)return null;

            var props=await session.TryGetMediaPropertiesAsync();
            var playback=session.GetPlaybackInfo();
            bool playing=playback?.PlaybackStatus==GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;

            string source=session.SourceAppUserModelId??"";
            return new(
                props?.Title??"",
                props?.Artist??"",
                props?.AlbumTitle??"",
                source,
                playing
            );
        } catch {
            failed=true;
            return null;
        }
    }

    public void Reset(){failed=false;manager=null;}
    public void Dispose(){manager=null;}
}
