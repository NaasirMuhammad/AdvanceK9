using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Linq;
using NAudio.Wave;
using Rage;

namespace AdvancedK9
{
    internal sealed class VoiceCommandService : IDisposable
    {
        private readonly string _endpoint, _model, _language, _apiKey; private string _dogName;
        private readonly HttpClient _client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        private WaveInEvent _input;
        private WaveFileWriter _writer;
        private MemoryStream _audio;
        private DateTime _started;
        private bool _continuous, _speechDetected, _segmenting;
        private DateTime _lastVoice;

        public bool IsAvailable { get; }
        public bool IsRecording => _input != null;
        public event Action<K9Command> CommandRecognized;
        public event Action<string> StatusChanged;

        public VoiceCommandService(string provider, string model, string language, string keyVariable, string dogName)
        {
            bool groq = provider.Equals("Groq", StringComparison.OrdinalIgnoreCase);
            _endpoint = groq ? "https://api.groq.com/openai/v1/audio/transcriptions" : "https://api.openai.com/v1/audio/transcriptions";
            _model = model;
            _language = language;
            _dogName = dogName;
            _apiKey = Environment.GetEnvironmentVariable(keyVariable);
            IsAvailable = !string.IsNullOrWhiteSpace(_apiKey);
            Game.LogTrivial(IsAvailable
                ? "AdvancedK9: AI voice ready using " + provider + "/" + model + "."
                : "AdvancedK9: AI voice disabled; " + keyVariable + " is not set. Keyboard commands remain active.");
        }

        public void StartRecording()
        {
            if (!IsAvailable || IsRecording) return;
            try
            {
                _audio = new MemoryStream();
                _input = new WaveInEvent { WaveFormat = new WaveFormat(16000, 1), BufferMilliseconds = 100 };
                _writer = new WaveFileWriter(new NonClosingStream(_audio), _input.WaveFormat);
                _input.DataAvailable += OnData;
                _started = DateTime.UtcNow;
                _input.StartRecording();
                StatusChanged?.Invoke("Listening");
                Game.DisplaySubtitle("~b~AI K9 voice: listening…~s~ Release push-to-talk to send.");
            }
            catch (Exception ex)
            {
                Game.LogTrivial("AdvancedK9 microphone error: " + ex.Message);
                Cleanup();
            }
        }

        public void StartContinuous(){if(!IsAvailable||IsRecording)return;_continuous=true;_speechDetected=false;_segmenting=false;StartRecording();}
        public void UpdateWakeWord(string value){if(!string.IsNullOrWhiteSpace(value))_dogName=value.Trim();}

        public void StopAndTranscribe()
        {
            if (!IsRecording) return;
            var elapsed = DateTime.UtcNow - _started;
            try { _input.StopRecording(); } catch { }
            _writer.Dispose(); // Finalizes the WAV header; wrapper keeps MemoryStream open.
            byte[] bytes = _audio.ToArray();
            Cleanup();
            if (elapsed.TotalMilliseconds < 350 || bytes.Length < 1000) return;
            Game.DisplaySubtitle("~b~AI K9 voice: transcribing…");
            System.Threading.Tasks.Task.Run(() => TranscribeAsync(bytes));
        }

        private void OnData(object sender, WaveInEventArgs e)
        {
            try { _writer?.Write(e.Buffer, 0, e.BytesRecorded); _writer?.Flush(); if(_continuous&&!_segmenting){int peak=0;for(int i=0;i+1<e.BytesRecorded;i+=2){int sample=Math.Abs((short)(e.Buffer[i]|(e.Buffer[i+1]<<8)));if(sample>peak)peak=sample;}if(peak>1100){_speechDetected=true;_lastVoice=DateTime.UtcNow;StatusChanged?.Invoke("Voice detected");}else if(_speechDetected&&(DateTime.UtcNow-_lastVoice).TotalMilliseconds>750&&(DateTime.UtcNow-_started).TotalMilliseconds>500){_segmenting=true;System.Threading.Tasks.Task.Run(()=>StopAndTranscribe());}} }
            catch (Exception ex) { Game.LogTrivial("AdvancedK9 capture error: " + ex.Message); }
        }

        private async System.Threading.Tasks.Task TranscribeAsync(byte[] wave)
        {
            try
            {
                StatusChanged?.Invoke("Recognizing");
                using (var form = new MultipartFormDataContent())
                using (var audio = new ByteArrayContent(wave))
                using (var request = new HttpRequestMessage(HttpMethod.Post, _endpoint))
                {
                    audio.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
                    form.Add(audio, "file", "k9-command.wav");
                    form.Add(new StringContent(_model), "model");
                    form.Add(new StringContent(_language), "language");
                    form.Add(new StringContent("text"), "response_format");
                    form.Add(new StringContent("The police dog is named " + _dogName + ". Commands begin with " + _dogName + " or K9: follow, heel, sit, down, search, track, get him, recall, fetch, leash, camera, academy."), "prompt");
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
                    request.Content = form;
                    using (var response = await _client.SendAsync(request).ConfigureAwait(false))
                    {
                        string text = (await response.Content.ReadAsStringAsync().ConfigureAwait(false)).Trim();
                        if (!response.IsSuccessStatusCode)
                        {
                            Game.LogTrivial("AdvancedK9 AI voice HTTP " + (int)response.StatusCode + ": " + Short(text));
                            Game.DisplayNotification("~r~AI voice request failed.~s~ Keyboard commands remain available.");
                            return;
                        }
                        K9Command command;
                        if (CommandRegistry.TryMatch(text, _dogName, out command))
                        {
                            Game.DisplayNotification("~b~AI heard:~s~ “" + text + "”");
                            CommandRecognized?.Invoke(command);
                            StatusChanged?.Invoke("Recognized: "+CommandRegistry.All.First(x=>x.Command==command).Label);
                        }
                        else { Game.DisplayNotification("~o~AI command not recognized:~s~ “" + text + "”"); StatusChanged?.Invoke("Not recognized"); }
                    }
                }
            }
            catch (Exception ex)
            {
                Game.LogTrivial("AdvancedK9 AI transcription error: " + ex.Message);
                Game.DisplayNotification("~r~AI voice unavailable.~s~ Keyboard commands remain available.");
            }
            finally { if(_continuous){await System.Threading.Tasks.Task.Delay(250).ConfigureAwait(false);_speechDetected=false;_segmenting=false;StartRecording();} }
        }

        private static string Short(string text) => text.Length > 300 ? text.Substring(0, 300) : text;

        private void Cleanup()
        {
            if (_input != null) _input.DataAvailable -= OnData;
            _input?.Dispose(); _writer?.Dispose(); _audio?.Dispose();
            _input = null; _writer = null; _audio = null;
        }

        public void Dispose() { _continuous=false; Cleanup(); _client.Dispose(); }

        private sealed class NonClosingStream : Stream
        {
            private readonly Stream _stream;
            public NonClosingStream(Stream stream) { _stream = stream; }
            public override bool CanRead => _stream.CanRead;
            public override bool CanSeek => _stream.CanSeek;
            public override bool CanWrite => _stream.CanWrite;
            public override long Length => _stream.Length;
            public override long Position { get => _stream.Position; set => _stream.Position = value; }
            public override void Flush() => _stream.Flush();
            public override int Read(byte[] b, int o, int c) => _stream.Read(b, o, c);
            public override long Seek(long o, SeekOrigin so) => _stream.Seek(o, so);
            public override void SetLength(long v) => _stream.SetLength(v);
            public override void Write(byte[] b, int o, int c) => _stream.Write(b, o, c);
            protected override void Dispose(bool disposing) { }
        }
    }
}
