using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Concurrent;
using NAudio.Wave;
using Rage;

namespace AdvancedK9
{
    internal sealed class VoiceCommandService : IDisposable
    {
        private readonly string _endpoint, _model, _language, _apiKey; private string _dogName;
        private readonly bool _showStatusText;
        private readonly HttpClient _client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        private WaveInEvent _input;
        private WaveFileWriter _writer;
        private MemoryStream _audio;
        private DateTime _started;
        private bool _continuous, _speechDetected, _segmenting;
        private DateTime _lastVoice;
        private readonly ConcurrentQueue<VoiceResult> _results = new ConcurrentQueue<VoiceResult>();
        private readonly object _captureSync = new object();
        private volatile bool _restartRequested;

        public bool IsAvailable { get; }
        public bool IsRecording => _input != null;
        public event Action<K9Command> CommandRecognized;
        public event Action<string> StatusChanged;

        public VoiceCommandService(string provider, string model, string language, string directKey, string keyVariable, string dogName, bool showStatusText)
        {
            // RPH hosts .NET Framework in a process where the system default can still be
            // TLS 1.0. Groq and OpenAI require TLS 1.2 or newer.
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            bool groq = provider.Equals("Groq", StringComparison.OrdinalIgnoreCase);
            _endpoint = groq ? "https://api.groq.com/openai/v1/audio/transcriptions" : "https://api.openai.com/v1/audio/transcriptions";
            _model = model;
            _language = language;
            _dogName = dogName;
            _showStatusText = showStatusText;
            _apiKey = string.IsNullOrWhiteSpace(directKey) ? Environment.GetEnvironmentVariable(keyVariable) : directKey.Trim();
            IsAvailable = !string.IsNullOrWhiteSpace(_apiKey);
            Game.LogTrivial(IsAvailable
                ? "AdvancedK9: AI voice ready using " + provider + "/" + model + "."
                : "AdvancedK9: AI voice disabled; no API key is configured. Keyboard commands remain active.");
        }

        public void StartRecording()
        {
            if (!IsAvailable || IsRecording) return;
            try
            {
                lock (_captureSync)
                {
                    if (_input != null) return;
                    _audio = new MemoryStream();
                    _input = new WaveInEvent { WaveFormat = new WaveFormat(16000, 1), BufferMilliseconds = 100 };
                    _writer = new WaveFileWriter(new NonClosingStream(_audio), _input.WaveFormat);
                    _input.DataAvailable += OnData;
                    _started = DateTime.UtcNow;
                    _input.StartRecording();
                }
                StatusChanged?.Invoke("Listening");
                if (_showStatusText) Game.DisplaySubtitle("~b~AI K9 voice: listening…~s~ Release push-to-talk to send.");
            }
            catch (Exception ex)
            {
                Game.LogTrivial("AdvancedK9 microphone error: " + ex.Message);
                _results.Enqueue(VoiceResult.Failure("AdvancedK9 microphone error: " + ex.Message));
                Cleanup();
            }
        }

        public void StartContinuous(){if(!IsAvailable||IsRecording)return;_continuous=true;_speechDetected=false;_segmenting=false;StartRecording();}
        public void StopListening(){_continuous=false;try{_input?.StopRecording();}catch{}Cleanup();StatusChanged?.Invoke("Off");}
        public void UpdateWakeWord(string value){if(!string.IsNullOrWhiteSpace(value))_dogName=value.Trim();}

        public void StopAndTranscribe()
        {
            byte[] bytes;
            TimeSpan elapsed;
            lock (_captureSync)
            {
                if (_input == null || _writer == null || _audio == null) return;
                elapsed = DateTime.UtcNow - _started;
                try { _input.DataAvailable -= OnData; _input.StopRecording(); } catch { }
                _writer.Dispose(); // Finalizes the WAV header; wrapper keeps MemoryStream open.
                bytes = _audio.ToArray();
                _input.Dispose(); _audio.Dispose();
                _input = null; _writer = null; _audio = null;
            }
            if (elapsed.TotalMilliseconds < 350 || bytes.Length < 1000) return;
            _results.Enqueue(VoiceResult.Status("Recognizing", "~b~AI K9 voice: transcribing…"));
            System.Threading.Tasks.Task.Run(() => TranscribeAsync(bytes));
        }

        private void OnData(object sender, WaveInEventArgs e)
        {
            try { lock(_captureSync){if(_writer==null)return;_writer.Write(e.Buffer, 0, e.BytesRecorded);_writer.Flush(); if(_continuous&&!_segmenting){int peak=0;for(int i=0;i+1<e.BytesRecorded;i+=2){int sample=Math.Abs((short)(e.Buffer[i]|(e.Buffer[i+1]<<8)));if(sample>peak)peak=sample;}if(peak>1100){_speechDetected=true;_lastVoice=DateTime.UtcNow;_results.Enqueue(VoiceResult.Status("Voice detected", null));}else if(_speechDetected&&(DateTime.UtcNow-_lastVoice).TotalMilliseconds>750&&(DateTime.UtcNow-_started).TotalMilliseconds>500){_segmenting=true;System.Threading.Tasks.Task.Run(()=>StopAndTranscribe());}}} }
            catch (Exception ex) { _results.Enqueue(VoiceResult.Log("AdvancedK9 capture error: " + ex.Message)); }
        }

        private async System.Threading.Tasks.Task TranscribeAsync(byte[] wave)
        {
            int restartDelay = 1000;
            try
            {
                using (var form = new MultipartFormDataContent())
                using (var audio = new ByteArrayContent(wave))
                using (var request = new HttpRequestMessage(HttpMethod.Post, _endpoint))
                {
                    audio.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
                    form.Add(audio, "file", "k9-command.wav");
                    form.Add(new StringContent(_model), "model");
                    form.Add(new StringContent(_language), "language");
                    form.Add(new StringContent("text"), "response_format");
                    form.Add(new StringContent("0"), "temperature");
                    string custom=CommandRegistry.VoicePromptPhrases;
                    form.Add(new StringContent("Police K9 handler command audio. The dog is named " + _dogName + ". Preserve the wake word and command exactly. K9 may be spoken as K-9, K nine, kay nine, canine, or " + _dogName + ". Likely commands include deploy, dismiss, sit, down, stay, follow, heel, recall, vehicle search, area search, building search, clear the building, narcotics search, drug search, bomb search, explosives search, weapons search, gun search, track, reacquire trail, find the scent, K9 warning, warn the suspect, apprehend, attack, engage, arrest handoff, request perimeter, prisoner transport, request EMS, request bomb squad, release, guard, bark, fetch, enter vehicle, exit vehicle, leash, camera, inspect, first aid, feed, treat, give water, drink, hydrate, pet, praise, core training, narcotics training, explosives training, weapons training, academy, and certification."+(string.IsNullOrWhiteSpace(custom)?"":" User-defined command phrases include: "+custom+".")), "prompt");
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
                    request.Content = form;
                    using (var response = await _client.SendAsync(request).ConfigureAwait(false))
                    {
                        string text = (await response.Content.ReadAsStringAsync().ConfigureAwait(false)).Trim();
                        if (!response.IsSuccessStatusCode)
                        {
                            restartDelay = 10000;
                            _results.Enqueue(VoiceResult.Failure("AdvancedK9 AI voice HTTP " + (int)response.StatusCode + ": " + Short(text)));
                            return;
                        }
                        K9Command command;
                        if (CommandRegistry.TryMatch(text, _dogName, out command))
                        {
                            _results.Enqueue(VoiceResult.Command(command, text));
                        }
                        else { _results.Enqueue(VoiceResult.NotRecognized(text)); }
                    }
                }
            }
            catch (Exception ex)
            {
                restartDelay = 10000;
                _results.Enqueue(VoiceResult.Failure("AdvancedK9 AI transcription error: " + ex));
            }
            finally { if(_continuous){await System.Threading.Tasks.Task.Delay(restartDelay).ConfigureAwait(false);_speechDetected=false;_segmenting=false;_restartRequested=true;} }
        }

        // Must be called from the owning RAGE game fiber. Background microphone and HTTP
        // callbacks only enqueue plain data; they never invoke RAGE natives or game objects.
        public void Tick()
        {
            VoiceResult result;
            while (_results.TryDequeue(out result))
            {
                if (!string.IsNullOrWhiteSpace(result.LogText)) Game.LogTrivial(result.LogText);
                if (_showStatusText && !string.IsNullOrWhiteSpace(result.Subtitle)) Game.DisplaySubtitle(result.Subtitle);
                if ((result.IsError || _showStatusText) && !string.IsNullOrWhiteSpace(result.Notification)) Game.DisplayNotification(result.Notification);
                if (!string.IsNullOrWhiteSpace(result.StatusText)) StatusChanged?.Invoke(result.StatusText);
                if (result.HasCommand) CommandRecognized?.Invoke(result.RecognizedCommand);
            }
            if (_restartRequested && _continuous && !IsRecording)
            {
                _restartRequested = false;
                StartRecording();
            }
        }

        private static string Short(string text) => text.Length > 300 ? text.Substring(0, 300) : text;

        private void Cleanup()
        {
            lock (_captureSync)
            {
                if (_input != null) _input.DataAvailable -= OnData;
                _input?.Dispose(); _writer?.Dispose(); _audio?.Dispose();
                _input = null; _writer = null; _audio = null;
            }
        }

        public void Dispose() { StopListening(); _client.Dispose(); }

        private sealed class VoiceResult
        {
            public string LogText, Subtitle, Notification, StatusText;
            public bool HasCommand;
            public bool IsError;
            public K9Command RecognizedCommand;
            public static VoiceResult Log(string log) => new VoiceResult { LogText = log };
            public static VoiceResult Status(string status, string subtitle) => new VoiceResult { StatusText = status, Subtitle = subtitle };
            public static VoiceResult Failure(string log) => new VoiceResult { LogText = log, StatusText = "Request failed", Notification = "~r~AI voice unavailable.~s~ Keyboard commands remain available.", IsError = true };
            public static VoiceResult Command(K9Command command, string text) => new VoiceResult { HasCommand = true, RecognizedCommand = command, StatusText = "Recognized: " + CommandRegistry.All.First(x => x.Command == command).Label, Notification = "~b~AI heard:~s~ “" + text + "”" };
            public static VoiceResult NotRecognized(string text) => new VoiceResult { StatusText = "Not recognized", Notification = "~o~AI command not recognized:~s~ “" + text + "”" };
        }

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
