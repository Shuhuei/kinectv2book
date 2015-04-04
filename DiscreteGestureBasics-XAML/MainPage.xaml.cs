//---------------------------------------------------------------------------------------------------
// <copyright file="MainPage.xaml.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//
// <Description>
// This program tracks up to 6 people simultaneously.
// If a person is tracked, the associated gesture detector will determine if that person is seated or not.
// If any of the 6 positions are not in use, the corresponding gesture detector(s) will be paused
// and the 'Not Tracked' image will be displayed in the UI.
// </Description>
//----------------------------------------------------------------------------------------------------

namespace Microsoft.Samples.Kinect.DiscreteGestureBasics
{
    using System.Collections.Generic;
    using System.ComponentModel;
    using Windows.ApplicationModel.Resources;
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Controls;
    using WindowsPreview.Kinect;

    /// <summary>
    /// Main page のロジックの部分です。    
    /// </summary>
    public sealed partial class MainPage : Page, INotifyPropertyChanged
    {
        /// <summary> リソースをロードします。 </summary>
        private ResourceLoader resourceLoader = ResourceLoader.GetForCurrentView("Resources");

        /// <summary> Kinect センサー本体を扱うためのフィールドです。 </summary>
        private KinectSensor kinectSensor = null;

        /// <summary> 各人の情報を格納する配列のフィールドです。 (Kinect は、最大6人まで同時に情報を採取できます。) </summary>
        private Body[] bodies = null;

        /// <summary> 各フレームにおいて人の情報が格納されるフィールドです。</summary>
        private BodyFrameReader bodyFrameReader = null;

        /// <summary> アプリが適切に動作しているものかを表示するための情報を扱います。 </summary>
        private string statusText = null;

        /// <summary> KinectBodyView オブジェクトは、UI 上の画面左に表示される ViewBox を描画する際に必要なオブジェクトです。 </summary>
        private KinectBodyView kinectBodyView = null;

        /// <summary>  GestureDetector のリストでです。
        /// 1人の情報につき、GestureDetector が存在します。
        /// このクラスにより、各人の姿勢の状態を判定します。(最大 6 つとなります。) </summary>
        private List<GestureDetector> gestureDetectorList = null;

        public MainPage()
        {
            // MainPage を動作させるための必要な初期化を実施します。
            this.InitializeComponent();

            // Kinect センサーのオブジェクトを取得します。
            // このサンプルコードでは、一つまでの Kinect センサーをサポートしています。
            this.kinectSensor = KinectSensor.GetDefault();

            // Kinect センサーについて (USB 接続が切れてしまった等) 変化した場合に
            // "Sensor_IsAvailableChanged" の処理が実行されるようにイベントハンドラを登録します。
            this.kinectSensor.IsAvailableChanged += this.Sensor_IsAvailableChanged;

            // Kinect センサーを起動します。
            this.kinectSensor.Open();

            // Kinect センサーが適切に動作しているかを表示します。(アプリの画面左下に表示されます。)
            this.StatusText = this.kinectSensor.IsAvailable ? this.resourceLoader.GetString("RunningStatusText")
                                                            : this.resourceLoader.GetString("NoSensorStatusText");

            // 人の情報が含まれたフレームを、読み込むためのオブジェクトを bodyFrameReader に格納します。
            this.bodyFrameReader = this.kinectSensor.BodyFrameSource.OpenReader();

            // フレームが到着したことを示すイベント FrameArrived が発生した際に
            // Reader_BodyFrameArrived の処理が実行されるようにイベントハンドラを登録します。 
            this.bodyFrameReader.FrameArrived += this.Reader_BodyFrameArrived;

            // アプリの画面左に表示されている人の情報を表示させるための UI BodyViewer オブジェクトを初期化します。
            this.kinectBodyView = new KinectBodyView(this.kinectSensor, this.bodyDisplayGrid);

            // 各人のジェスチャーを取得するためのオブジェクトである、GestureDetecter のリストを初期化します。
            this.gestureDetectorList = new List<GestureDetector>();

            // 各人毎に、GestureDetecte オブジェクトを作成、紐づけを実施します。
            // また、UI に表示を行うための、GestureResultView のオブジェクトの作成、設定もこの部分で行います。
            int col0Row = 0;
            int col1Row = 0;
            int maxBodies = this.kinectSensor.BodyFrameSource.BodyCount;
            for (int i = 0; i < maxBodies; ++i)
            {
                GestureResultView result = new GestureResultView(i, false, false, 0.0f);
                GestureDetector detector = new GestureDetector(this.kinectSensor, result);
                this.gestureDetectorList.Add(detector);


                // 画面左に 3 行 * 2 列 の合計 6 つの グリッドを作成します。
                // 各グリッドでは、人のジェスチャーの結果を表示します。
                ContentControl contentControl = new ContentControl();
                contentControl.ContentTemplate = this.GestureResultDataTemplate;
                contentControl.Content = this.gestureDetectorList[i].GestureResultView;

                if (i % 2 == 0)
                {
                    //6 つのグリッドのうち、左側に表示されるグリッドを定義します。
                    Grid.SetColumn(contentControl, 0);
                    Grid.SetRow(contentControl, col0Row);
                    ++col0Row;
                }
                else
                {
                    //6 つのグリッドのうち、右側に表示されるグリッドを定義します。
                    Grid.SetColumn(contentControl, 1);
                    Grid.SetRow(contentControl, col1Row);
                    ++col1Row;
                }

                this.contentGrid.Children.Add(contentControl);
            }

            // UI にデータをバインドするための、DataContext の設定を行います。
            this.bodyDisplayGrid.DataContext = this.kinectBodyView.DisplayGrid;
            this.DataContext = this;
        }

        /// <summary>
        /// INotifyPropertyChangedPropertyChanged はデータの設定変更を UI に通知するための、イベントです。
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// 現在の Kinect センサーの状態を適切に取得します。
        /// </summary>
        public string StatusText
        {
            get
            {
                return this.statusText;
            }

            set
            {
                if (this.statusText != value)
                {
                    this.statusText = value;

                    if (this.PropertyChanged != null)
                    {
                        this.PropertyChanged(this, new PropertyChangedEventArgs("StatusText"));
                    }
                }
            }
        }

        /// <summary>
        /// MainPage がアンロードされた時の処理です。
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void MainPage_Unloaded(object sender, RoutedEventArgs e)
        {
            if (this.bodyFrameReader != null)
            {
                // BodyFrameReader オブジェクトの解放処理を実施します。
                this.bodyFrameReader.FrameArrived -= this.Reader_BodyFrameArrived;
                this.bodyFrameReader.Dispose();
                this.bodyFrameReader = null;
            }

            if (this.gestureDetectorList != null)
            {
                // 各 GestureDetector オブジェクトの解放処理を実施します。
                foreach (GestureDetector detector in this.gestureDetectorList)
                {
                    detector.Dispose();
                }

                this.gestureDetectorList.Clear();
                this.gestureDetectorList = null;
            }

            //Kinect センサーのオブジェクトの解放処理を行います。
            if (this.kinectSensor != null)
            {
                this.kinectSensor.IsAvailableChanged -= this.Sensor_IsAvailableChanged;
                this.kinectSensor.Close();
                this.kinectSensor = null;
            }
        }

        /// <summary>
        /// Kinect センサーが、利用不可能になった時に呼び出されるイベントハンドラです。
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void Sensor_IsAvailableChanged(object sender, IsAvailableChangedEventArgs e)
        {
            // Kinect センサーが利用可能かどうか、判定します。
            if (!this.kinectSensor.IsAvailable)
            {
                this.StatusText = this.resourceLoader.GetString("SensorNotAvailableStatusText");
            }
            else
            {
                this.StatusText = this.resourceLoader.GetString("RunningStatusText");
            }
        }

        /// <summary>
        /// フレームが、Kinect センサーから送られてきた際に呼び出されるイベントハンドラです。
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void Reader_BodyFrameArrived(object sender, BodyFrameArrivedEventArgs e)
        {
            bool dataReceived = false;

            using (BodyFrame bodyFrame = e.FrameReference.AcquireFrame())
            {
                if (bodyFrame != null)
                {
                    if (this.bodies == null)
                    {
                        // 各人の情報が入る配列を定義する。最大同時に 6 人まで検知できるので、要素数 6 の配列を定義する。
                        this.bodies = new Body[bodyFrame.BodyCount];
                    }

                    // 最初に GetAndRefreshBodyData が呼ばれた時に、Kinect は各人の情報を配列に格納します。
                    // 各人のオブジェクトが解放され、null 値が設定されない限り、オブジェクトは再利用されます。
                    bodyFrame.GetAndRefreshBodyData(this.bodies);
                    dataReceived = true;
                }
            }

            if (dataReceived)
            {
                // 人のデータを可視化します。
                // 1人以上の人の情報が格納されていたら、人のオブジェクトに含まれている情報を更新し、画面に描画します。
                // これまでに追跡されていた人が、追跡されなくなった場合、画面上に描画を実行しないようにします。
                this.kinectBodyView.UpdateBodyFrame(this.kinectSensor, this.bodies);

                // もし人の情報を失ったり、検知した場合に、動作を検知する GestureDetector の紐づけを行います。
                if (this.bodies != null)
                {
                    // 現在存在するすべての人のオブジェクトに対して GestureDetector の紐づけを確認します。
                    // loop through all bodies to see if any of the gesture detectors need to be updated
                    int maxBodies = this.kinectSensor.BodyFrameSource.BodyCount;
                    for (int i = 0; i < maxBodies; ++i)
                    {
                        Body body = this.bodies[i];
                        ulong trackingId = body.TrackingId;

                        // もし、TrackId が、変わっている場合、GestureDetector の TrackId も変更します。
                        // if the current body TrackingId changed, update the corresponding gesture detector with the new value
                        if (trackingId != this.gestureDetectorList[i].TrackingId)
                        {
                            this.gestureDetectorList[i].TrackingId = trackingId;

                            // 現在チェックしている人が、追跡されている状態なら、GestureDetector の IsPaused の値に True を格納し、処理を続行する。
                            // 現在チェックしている人が、追跡されていない状態ならば、GestureDetector が不要なので、IsPaused に False を格納し処理を停止する。 
                            this.gestureDetectorList[i].IsPaused = trackingId == 0;
                        }
                    }
                }
            }
        }
    }
}
