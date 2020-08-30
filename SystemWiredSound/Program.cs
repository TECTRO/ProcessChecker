using System.Media;
using System.Threading;

namespace SystemWiredSound
{
    class Program
    {
        static void Main()
        {
            MuteChannel(0, true);
            MuteChannel(0, false, 1000);
            new SoundPlayer { Stream = Properties.Resources.vizg}.Play();
        }

        public static void MuteChannel(int channelIndex, bool isMute, int delayMilliseconds = 0)
        {
            new Thread(() =>
            {
                if (delayMilliseconds > 0) Thread.Sleep(delayMilliseconds);

                NAudio.CoreAudioApi.MMDeviceEnumerator mmde = new NAudio.CoreAudioApi.MMDeviceEnumerator();

                NAudio.CoreAudioApi.MMDeviceCollection devCol = mmde.EnumerateAudioEndPoints(NAudio.CoreAudioApi.DataFlow.All, NAudio.CoreAudioApi.DeviceState.All);

                foreach (NAudio.CoreAudioApi.MMDevice dev in devCol)
                {
                    try
                    {
                        if (dev.State == NAudio.CoreAudioApi.DeviceState.Active)
                            dev.AudioSessionManager.Sessions[dev.AudioSessionManager.Sessions.Count - channelIndex - 1].SimpleAudioVolume.Mute = isMute;
                    }
                    catch { /* ignored */ }
                }
            }).Start();

        }
    }
}
