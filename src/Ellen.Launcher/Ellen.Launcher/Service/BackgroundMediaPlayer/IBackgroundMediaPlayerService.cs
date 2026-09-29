using Microsoft.UI.Xaml.Controls;

namespace Ellen.Launcher.Service.BackgroundMediaPlayer;

public interface IBackgroundMediaPlayerService
{
    ValueTask UpdateMediaPlayerElementAsync(MediaPlayerElement element, CancellationToken token = default);

    void Pause();

    void Play();

    void Stop();
}
