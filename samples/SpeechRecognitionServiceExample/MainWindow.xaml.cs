    using Microsoft.Win32;
    using System;
    using System.ComponentModel;
    using System.Configuration;
    using System.Diagnostics;
    using System.IO;
    using System.IO.IsolatedStorage;
    using System.Runtime.CompilerServices;
    using System.Windows;
    using System.Windows.Forms;
    using System.Text;
    using System.Windows.Input;
    using Newtonsoft.Json;
    using System.Net;
    using System.Security.Cryptography;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Windows.Controls;
    using System.Windows.Data;
    using System.Windows.Documents;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using System.Windows.Navigation;
    using System.Windows.Shapes;


namespace md5
{
    public class md5
    {
        static String Md5(String s)
        {
            MD5 md5 = new MD5CryptoServiceProvider();
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(s);
            bytes = md5.ComputeHash(bytes);
            md5.Clear();
            string ret = "";
            for (int i = 0; i < bytes.Length; i++)
            {
                ret += Convert.ToString(bytes[i], 16).PadLeft(2, '0');
            }
            return ret.PadLeft(32, '0');
        }
    }
}

namespace Microsoft.CognitiveServices.SpeechRecognition
{

    public class TransObj
    {
        public string from { get; set; }
        public string to { get; set; }
        public List<TransResult> trans_result { get; set; }
    }
    public class TransResult
    {
        public string src { get; set; }
        public string dst { get; set; }
    }

    public partial class MainWindow : Window, INotifyPropertyChanged
    {

        private const string IsolatedStorageSubscriptionKeyFileName = "Subscription.txt";

        /// <summary>
        ///也可以将主键放在app.config中，而不是使用UI。
        /// string subscriptionKey = ConfigurationManager.AppSettings["primaryKey"];
        /// </summary>
        private string subscriptionKey;

        /// <summary>
        /// 数据识别客户端
        /// </summary>
        private DataRecognitionClient dataClient;

        /// <summary>
        /// 麦克风客户端
        /// </summary>
        private MicrophoneRecognitionClient micClient;

        /// <summary>
        /// 初始化<see cref =“MainWindow”/>类的新实例。
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
            this.InitializeComponent();
            this.Initialize();
        }
   

      

        #region Events

        /// <summary>
        /// 实现INotifyPropertyChanged接口
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        #endregion Events

        /// <summary>
        /// 获取或设置一个值，指示此实例是否是麦克风客户端短语。
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance is microphone client short phrase; otherwise, <c>false</c>.
        /// </value>
        public bool IsMicrophoneClientShortPhrase { get; set; }

        /// <summary>
        /// 获取或设置一个值，指示此实例是否为麦克风客户端听写。
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance is microphone client dictation; otherwise, <c>false</c>.
        /// </value>
        public bool IsMicrophoneClientDictation { get; set; }

        /// <summary>
        ///获取或设置一个值，该值指示此实例是否是数据客户端短语。
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance is data client short phrase; otherwise, <c>false</c>.
        /// </value>
        public bool IsDataClientShortPhrase { get; set; }

        /// <summary>
        /// 获取或设置一个值，指示此实例是否是数据客户端听写。
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance is data client dictation; otherwise, <c>false</c>.
        /// </value>
        public bool IsDataClientDictation { get; set; }

        /// <summary>
        /// 获取或设置订阅密钥
        /// </summary>
        public string SubscriptionKey
        {
            get
            {
                return this.subscriptionKey;
            }

            set
            {
                this.subscriptionKey = value;
                this.OnPropertyChanged<string>();
            }
        }

        /// <summary>
        ///获取指示是否使用麦克风的值。
        /// </summary>
        /// <value>
        ///   <c>true</c> if [use microphone]; otherwise, <c>false</c>.
        /// </value>
        private bool UseMicrophone
        {
            get
            {
                return
                    this.IsMicrophoneClientDictation ||
                    this.IsMicrophoneClientShortPhrase;
            }
        }

        /// <summary>
        /// 获取当前的语音识别模式。
        /// </summary>
        /// <value>
        /// 语音识别模式。
        /// </value>
        private SpeechRecognitionMode Mode
        {
            get
            {
                if (this.IsMicrophoneClientDictation ||
                    this.IsDataClientDictation)
                {
                    return SpeechRecognitionMode.LongDictation;
                }

                return SpeechRecognitionMode.ShortPhrase;
            }
        }

        /// <summary>
        /// 获取默认语言环境。
        /// </summary>
        /// <value>
        /// 默认的语言环境。
        /// </value>
        private string language;

        private string DefaultLocale
        {
            get { return language; }
        }

        /// <summary>
        /// 获取短波文件路径。
        /// </summary>
        /// <value>
        /// 短波文件。
        /// </value>
        private string ShortWaveFile
        {
            get
            {
                return ConfigurationManager.AppSettings["ShortWaveFile"];
            }
        }

        /// <summary>
        /// 获取长波文件路径。
        /// </summary>
        /// <value>
        /// 长波文件
        /// </value>
        private string LongWaveFile
        {
            get
            {
                return ConfigurationManager.AppSettings["LongWaveFile"];
            }
        }

        /// <summary>
        /// 获得认知服务认证Uri。
        /// </summary>
        /// <value>
        /// 认知服务认证Uri。 如果要使用全局默认值，则为空。
        /// </value>
        private string AuthenticationUri
        {
            get
            {
                return ConfigurationManager.AppSettings["AuthenticationUri"];
            }
        }

        /// <summary>
        /// 引发System.Windows.Window.Closed事件。
        /// </summary>
        /// <param name="e">包含事件数据的System.EventArgs。.</param>
        protected override void OnClosed(EventArgs e)
        {
            if (null != this.dataClient)
            {
                this.dataClient.Dispose();
            }

            if (null != this.micClient)
            {
                this.micClient.Dispose();
            }

            base.OnClosed(e);
        }


        /// <summary>
        /// 初始化新的音频会话。
        /// </summary>
        private void Initialize()
        {
            this.IsMicrophoneClientShortPhrase = true;
            this.IsMicrophoneClientDictation = false;
            this.IsDataClientShortPhrase = false;
            this.IsDataClientDictation = false;

            // 设置复选框组的默认选项。
            this._micRadioButton.IsChecked = true;

            this.SubscriptionKey = this.GetSubscriptionKeyFromIsolatedStorage();
        }

        /// <summary>
        /// 处理_startButton控件的Click事件。
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing the event data.</param>

        private bool stopFlag = true;

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            stopFlag = false;
            this._startButton.IsEnabled = false;
            this._radioGroup.IsEnabled = false;

            this.LogRecognitionStart();

            if (this.UseMicrophone)
            {
                if (this.micClient == null)
                {
                    this.CreateMicrophoneRecoClient();
                }

                this.micClient.StartMicAndRecognition();
            }
            else
            {
                if (null == this.dataClient)
                {
                    this.CreateDataRecoClient();
                }

                this.SendAudioHelper((this.Mode == SpeechRecognitionMode.ShortPhrase) ? this.ShortWaveFile : this.LongWaveFile);
            }
        }

        /// <summary>
         private string language1;

        /// 记录识别开始。
        /// </summary>
        private void LogRecognitionStart()
        {
            string recoSource;
            if (this.UseMicrophone)
            {
                recoSource = "麦克风";
            }
            else if (this.Mode == SpeechRecognitionMode.ShortPhrase)
            {
                recoSource = "短句";
            }
            else
            {
                recoSource = "长句";
            }

            if (language == "ZH-CN")
                language1 = "中文";
            if(language == "EN-GB")
                language1 = "英文";
            if(language == "JA-JP")
                language1 = "日文";
            this.WriteLine("\n--- 开始使用语音识别" + recoSource + "的 " + this.Mode + "使用 " + language1 + " 语言 ----\n\n");
        }

        /// <summary>
        /// 创建一个没有LUIS意图支持的新的麦克风录音客户端。
        /// </summary>
        private void CreateMicrophoneRecoClient()
        {
            this.micClient = SpeechRecognitionServiceFactory.CreateMicrophoneClient(
                this.Mode,
                this.DefaultLocale,
                this.SubscriptionKey);
            this.micClient.AuthenticationUri = this.AuthenticationUri;

            // 语音识别结果的事件处理程序
            this.micClient.OnMicrophoneStatus += this.OnMicrophoneStatus;
            this.micClient.OnPartialResponseReceived += this.OnPartialResponseReceivedHandler;
            if (this.Mode == SpeechRecognitionMode.ShortPhrase)
            {
                this.micClient.OnResponseReceived += this.OnMicShortPhraseResponseReceivedHandler;
            }
            else if (this.Mode == SpeechRecognitionMode.LongDictation)
            {
                this.micClient.OnResponseReceived += this.OnMicDictationResponseReceivedHandler;
            }

            this.micClient.OnConversationError += this.OnConversationErrorHandler;
        }

        /// <summary>
        ///创建一个没有LUIS意图支持的数据客户端。
        ///用数据进行语音识别（例如从文件或音频源）。
        ///数据被分解成缓冲区，每个缓冲区被发送到语音识别服务。
        ///没有对缓冲区进行修改，所以用户可以应用它们
        ///自己的沉默检测，如果需要的话。
        /// </summary>
        private void CreateDataRecoClient()
        {
            this.dataClient = SpeechRecognitionServiceFactory.CreateDataClient(
                this.Mode,
                this.DefaultLocale,
                this.SubscriptionKey);
            this.dataClient.AuthenticationUri = this.AuthenticationUri;

            // 语音识别结果的事件处理程序
            if (this.Mode == SpeechRecognitionMode.ShortPhrase)
            {
                this.dataClient.OnResponseReceived += this.OnDataShortPhraseResponseReceivedHandler;
            }
            else
            {
                this.dataClient.OnResponseReceived += this.OnDataDictationResponseReceivedHandler;
            }

            this.dataClient.OnPartialResponseReceived += this.OnPartialResponseReceivedHandler;
            this.dataClient.OnConversationError += this.OnConversationErrorHandler;
        }

        /// <summary>
        /// 发送音频助手。
        /// </summary>
        /// <param name="wavFileName">Name of the wav file.</param>
        private void SendAudioHelper(string wavFileName)
        {
            using (FileStream fileStream = new FileStream(wavFileName, FileMode.Open, FileAccess.Read))
            {
                //注意波形文件，我们可以直接从文件发送数据到服务器。
                //如果你不是wave格式的音频文件，而是你刚才
                //原始数据（例如通过蓝牙传来的音频），然后发送任何
                //音频数据，您必须首先发送一个SpeechAudioFormat描述符来描述
                //通过DataRecognitionClient的sendAudioFormat（）方法，您的原始音频数据的布局和格式。
                int bytesRead = 0;
                byte[] buffer = new byte[1024];

                try
                {
                    do
                    {
                        // 获取更多的音频数据发送到字节缓冲区。
                        bytesRead = fileStream.Read(buffer, 0, buffer.Length);

                        // 发送音频数据到服务。
                        this.dataClient.SendAudio(buffer, bytesRead);
                    }
                    while (bytesRead > 0);
                }
                finally
                {
                    // 我们正在发送音频。 最终识别结果将在OnResponseReceived事件调用中到达。
                    this.dataClient.EndAudio();
                }
            }
        }

        /// <summary>
        /// 在收到最终答复时调用;
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="SpeechResponseEventArgs"/> instance containing the event data.</param>
        private void OnMicShortPhraseResponseReceivedHandler(object sender, SpeechResponseEventArgs e)
        {
            Dispatcher.Invoke((Action)(() =>
            {
                this.WriteLine("文本结果");

                //我们得到了最后的结果，所以我们可以结束麦克风录音。 没有必要这样做
                //对于dataReco来说，因为我们一旦完成就已经调用了endAudio（）
                //发送所有的数据

                this.micClient.EndMicAndRecognition();

                this.WriteResponseResult(e);

                _startButton.IsEnabled = true;
                _radioGroup.IsEnabled = true;
            }));
        }

        /// <summary>
        /// 在收到最终答复时调用;
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="SpeechResponseEventArgs"/> instance containing the event data.</param>
        private void OnDataShortPhraseResponseReceivedHandler(object sender, SpeechResponseEventArgs e)
        {
            Dispatcher.Invoke((Action)(() =>
            {
                this.WriteLine("短语结果");

                //我们得到了最后的结果，所以我们可以结束麦克风录音。 没有必要这样做
                //对于dataReco来说，因为我们一旦完成就已经调用了endAudio（）
                //发送所有的数据
                this.WriteResponseResult(e);

                _startButton.IsEnabled = true;
                _radioGroup.IsEnabled = true;
            }));
        }

        /// <summary>
        /// 写入响应结果。
        /// </summary>
        /// <param name="e">The <see cref="SpeechResponseEventArgs"/> instance containing the event data.</param>
        private void WriteResponseResult(SpeechResponseEventArgs e)
        {

            if (e.PhraseResponse.Results.Length == 0)
            {
                this.WriteLine("没有收到语音");
            }
            else
            {
                this.WriteLine("*********最终建议文本*********");

                for (int i = 0; i < e.PhraseResponse.Results.Length; i++)
                {
                    this.WriteLine(
                        "[建议{0}] , Text=\"{2}\"",
                        i,
                        e.PhraseResponse.Results[i].Confidence,
                        e.PhraseResponse.Results[i].DisplayText);

                }
                if (IsMicrophoneClientDictation == true || IsDataClientDictation == true)
                {
                    this.WriteLine("正在继续读取语音");
                }
                else if (IsMicrophoneClientShortPhrase == true || IsDataClientShortPhrase == true)
                {
                    this.WriteLine("结束");
                }
                this.WriteLine();
            }
        }

        /// <summary>
        /// 在收到最终答复时调用;
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="SpeechResponseEventArgs"/> instance containing the event data.</param>
        private void OnMicDictationResponseReceivedHandler(object sender, SpeechResponseEventArgs e)
        {
            this.WriteLine("文本结果");
            if (e.PhraseResponse.RecognitionStatus == RecognitionStatus.EndOfDictation ||
                e.PhraseResponse.RecognitionStatus == RecognitionStatus.DictationEndSilenceTimeout)
            {
                Dispatcher.Invoke(
                    (Action)(() =>
                    {
                        //我们得到了最后的结果，所以我们可以结束麦克风录音。 没有必要这样做
                        //对于dataReco来说，因为我们一旦完成就已经调用了endAudio（）
                        //发送所有的数据
                        this.micClient.EndMicAndRecognition();

                        this._startButton.IsEnabled = true;
                        this._radioGroup.IsEnabled = true;
                    }));
            }
            this.WriteResponseResult(e);
        }

        /// <summary>
        /// 在收到最终答复时调用;
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="SpeechResponseEventArgs"/> instance containing the event data.</param>
        private void OnDataDictationResponseReceivedHandler(object sender, SpeechResponseEventArgs e)
        {
            this.WriteLine("--- OnDataDictationResponseReceivedHandler ---");
            if (e.PhraseResponse.RecognitionStatus == RecognitionStatus.EndOfDictation ||
                e.PhraseResponse.RecognitionStatus == RecognitionStatus.DictationEndSilenceTimeout)
            {
                Dispatcher.Invoke(
                    (Action)(() =>
                    {
                        _startButton.IsEnabled = true;
                        _radioGroup.IsEnabled = true;

                        //我们得到了最后的结果，所以我们可以结束麦克风录音。 没有必要这样做
                        //对于dataReco来说，因为我们一旦完成就已经调用了endAudio（）
                        //发送所有的数据
                    }));
            }

            this.WriteResponseResult(e);
        }


        /// <summary>
        /// 在收到部分响应时调用。
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="PartialSpeechResponseEventArgs"/> instance containing the event data.</param>
        private void OnPartialResponseReceivedHandler(object sender, PartialSpeechResponseEventArgs e)
        {

        }

        /// <summary>
        /// 在收到错误时调用。
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="SpeechErrorEventArgs"/> instance containing the event data.</param>
        private void OnConversationErrorHandler(object sender, SpeechErrorEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                _startButton.IsEnabled = true;
                _radioGroup.IsEnabled = true;
            });

            this.WriteLine("--- 出现错误 ---");
            this.WriteLine("错误代码: {0}", e.SpeechErrorCode.ToString());
            this.WriteLine("错误文本: {0}", e.SpeechErrorText);
            this.WriteLine();
        }

        /// <summary>
        /// 麦克风状态改变时调用。
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="MicrophoneEventArgs"/> instance containing the event data.</param>
        private void OnMicrophoneStatus(object sender, MicrophoneEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                WriteLine("", e.Recording);
                if (e.Recording)
                {
                    WriteLine("请开始你的表演");
                }

                WriteLine("\n");
            });
        }

        /// <summary>
        ///写行。
        /// </summary>
        private void WriteLine()
        {
            this.WriteLine(string.Empty);
        }

        /// <summary>
        /// 写行.
        /// </summary>
        /// <param name="format">The format.</param>
        /// <param name="args">The arguments.</param>
        private void WriteLine(string format, params object[] args)
        {
            var formattedStr = string.Format(format, args);
            Trace.WriteLine(formattedStr);
            Dispatcher.Invoke(() =>
            {
                _logText.Text += (formattedStr + "\n");
                _logText.ScrollToEnd();
            });
        }

        /// <summary>
        /// 从隔离存储获取订阅密钥。
        /// </summary>
        /// <returns>The subscription key.</returns>
        private string GetSubscriptionKeyFromIsolatedStorage()
        {
            string subscriptionKey = null;

            using (IsolatedStorageFile isoStore = IsolatedStorageFile.GetStore(IsolatedStorageScope.User | IsolatedStorageScope.Assembly, null, null))
            {
                try
                {
                    using (var iStream = new IsolatedStorageFileStream(IsolatedStorageSubscriptionKeyFileName, FileMode.Open, isoStore))
                    {
                        using (var reader = new StreamReader(iStream))
                        {
                            subscriptionKey = reader.ReadLine();
                        }
                    }
                }
                catch (FileNotFoundException)
                {
                    subscriptionKey = null;
                }
            }



            return subscriptionKey;
        }

        /// <summary>
        /// INotifyPropertyChanged接口的辅助函数
        /// </summary>
        /// <typeparam name="T">Property type</typeparam>
        /// <param name="caller">Property name</param>
        private void OnPropertyChanged<T>([CallerMemberName]string caller = null)
        {
            var handler = this.PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(caller));
            }
        }

        /// <summary>
        /// 处理RadioButton控件的Click事件。
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing the event data.</param>
        private void RadioButton_Click(object sender, RoutedEventArgs e)
        {
            // 重置一切
            if (this.micClient != null)
            {
                this.micClient.EndMicAndRecognition();
                this.micClient.Dispose();
                this.micClient = null;
            }

            if (this.dataClient != null)
            {
                this.dataClient.Dispose();
                this.dataClient = null;
            }

            this._logText.Text = string.Empty;
            this._startButton.IsEnabled = true;
            this._radioGroup.IsEnabled = true;
        }




        private void _micRadioButton_Checked(object sender, RoutedEventArgs e)
        {

        }

        private void Button1_Clicked(object sender, RoutedEventArgs e)
        {
            language = "ZH-CN";
        }

        private void Button2_Click(object sender, RoutedEventArgs e)
        {
            language = "EN-GB";
        }

        private void Button3_Click(object sender, RoutedEventArgs e)
        {
            language = "JA-JP";
        }

        private void button1_Click(object sender, RoutedEventArgs e)
        {
            WebClient client = new WebClient();
            string txtInput = txtWord.Text;
            txtInput = txtInput.Replace(@"#", "%23");

            string str = "20171118000097022" + txtInput + "1435660288HRA9xQPOW7R0QsOLCElU";
            string sign = Md5(str);

            string url = string.Format("http://api.fanyi.baidu.com/api/trans/vip/translate?q={0}&from=en&to=zh&appid=20171118000097022&salt=1435660288&sign={1}", txtInput, sign);
            var buffer = client.DownloadData(url);
            string result = Encoding.UTF8.GetString(buffer);
            StringReader sr = new StringReader(result);
            JsonTextReader jsonReader = new JsonTextReader(sr);
            JsonSerializer serializer = new JsonSerializer();
            var r = serializer.Deserialize<TransObj>(jsonReader);
            txtResult.Text = r.trans_result[0].dst;
        }

        static String Md5(String s)
        {
            MD5 md5 = new MD5CryptoServiceProvider();
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(s);
            bytes = md5.ComputeHash(bytes);
            md5.Clear();
            string ret = "";
            for (int i = 0; i < bytes.Length; i++)
            {
                ret += Convert.ToString(bytes[i], 16).PadLeft(2, '0');
            }
            return ret.PadLeft(32, '0');
        }




        
    }
}
