//------------------------------------------------------------------------------
// <copyright file="MainPage.xaml.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel.Resources;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;
using WindowsPreview.Kinect;

namespace Microsoft.Samples.Kinect.InfraredBasics
{
    /// <summary>
    /// Main page のロジックの部分です。
    /// </summary> 
    public sealed partial class MainPage : Page, INotifyPropertyChanged
    {
        /// <summary>
        /// InfraredSourceValueMaximum は、InfraredFrame (赤外線で取得した情報が含まれるフレーム。以下赤外線フレーム) のピクセルの輝度の上限値が入ります。
        /// 可読性を高めるため、float 型へのキャストが可能です。
        /// </summary>
        private const float InfraredSourceValueMaximum = (float)ushort.MaxValue;

        /// <summary>
        /// InfraredOutputValueMinimum は、赤外線フレームのピクセルの下限値が入ります。
        /// </summary>
        private const float InfraredOutputValueMinimum = 0.01f;

        /// <summary>
        /// InfraredOutputValueMaximum は、赤外線データを描画用のデータとして処理してからの上限値です。
        /// </summary>
        private const float InfraredOutputValueMaximum = 1.0f;

        /// <summary>
        /// InfraredSceneValueAverage は、赤外線データが保持している値の平均値が入っています。
        /// この値は、与えられたフレームの各ピクセルの輝度の平均を格納します。
        /// 可視化する際の条件によりますが、この値は、定数として格納するか、
        /// 描画する際の輝度の平均値を計算することで得られます。
        /// </summary>
        private const float InfraredSceneValueAverage = 0.08f;

        /// <summary>
        /// InfraredSceneStandardDeviations は、InfraredSceneValueAverage を元に算出された標準偏差となります。
        /// この値は与えられた条件から、データを解析して算出されます。
        /// InfraredSceneValueAverage と同じくアプリケーションの要件に依存しますが、
        /// この値は定数としてハードコードするか実行時に計算させるこにより、得られます。
        /// </summary>
        private const float InfraredSceneStandardDeviations = 3.0f;

        /// <summary>
        /// ビットマップ形式のデータを格納する際、インデックスに使用します。
        /// </summary>
        private const int BytesPerPixel = 4;

    
#if WIN81ORLATER
        private ResourceLoader resourceLoader = ResourceLoader.GetForCurrentView("Resources");
#else
        private ResourceLoader resourceLoader = new ResourceLoader("Resources");
#endif

        /// <summary>
        /// Kinect センサー本体を扱うためのフィールドです。
        /// </summary>
        private KinectSensor kinectSensor = null;

        /// <summary>
        /// 赤外線センサーから届くフレームを扱うためのフィールドです。
        /// </summary>
        private InfraredFrameReader infraredFrameReader = null;

        /// <summary>
        /// ビットマップ形式のデータを画面上に表示させるためのフィールドです。
        /// </summary>
        private WriteableBitmap bitmap = null;

        /// <summary>
        /// センサーからのフレームデータを受け取るための中間ストレージとして機能するフィールドです。
        /// </summary>
        private ushort[] infraredFrameData = null;

        /// <summary>
        /// フレームデータ用の中間サーバーのデータをカラーに変換するためのフィールドです。
        /// </summary>
        private byte[] infraredPixels = null;

        /// <summary>
        /// アプリが適切に動作しているものかを表示するための情報を扱います。
        /// </summary>
        private string statusText = null;

        /// <summary>
        /// MainPage クラスです。
        /// </summary>
        public MainPage()
        {
            // Kinect センサーのオブジェクトを取得します。
            this.kinectSensor = KinectSensor.GetDefault();

            // 赤外線フレームに関する情報が格納されたオブジェクトを取得します。
            FrameDescription infraredFrameDescription = this.kinectSensor.InfraredFrameSource.FrameDescription;

            // 赤外線フレームを読み込むための Reader を開きます。
            this.infraredFrameReader = this.kinectSensor.InfraredFrameSource.OpenReader();

            // フレームが到着したことを示すイベント "FrameArrived" が発生した際に
            this.infraredFrameReader.FrameArrived += this.Reader_InfraredFrameArrived;

            // 512 × 424 サイズの配列を定義します。
            this.infraredFrameData = new ushort[infraredFrameDescription.Width * infraredFrameDescription.Height];
            this.infraredPixels = new byte[infraredFrameDescription.Width * infraredFrameDescription.Height * BytesPerPixel];

            // ディスプレイに表示するための ビットマップデータを出力します。
            this.bitmap = new WriteableBitmap(infraredFrameDescription.Width, infraredFrameDescription.Height);

            // Kinect センサーについて (USB 接続が切れてしまった等) 変化した場合に
            // "Sensor_IsAvailableChanged" の処理が実行されるようにイベントハンドラを登録します。
            this.kinectSensor.IsAvailableChanged += this.Sensor_IsAvailableChanged;

            // Kinect Sensor の処理を開始します。
            this.kinectSensor.Open();

            // Kinect センサーが適切に動作しているものかを表示します。(アプリの画面左下に表示されます。)
            this.StatusText = this.kinectSensor.IsAvailable ? resourceLoader.GetString("RunningStatusText")
                                                            : resourceLoader.GetString("NoSensorStatusText");

            // View Model として、扱えるように DataContext に MainPage クラスを指定します。
            this.DataContext = this;

            // アプリの起動に必要な初期化処理を実行します。
            this.InitializeComponent();
        }

        /// <summary>
        /// INotifyPropertyChangedPropertyChanged を用いて、プロパティの変更を画面コントロールに通知します。 
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// 現在の Kinect センサーの状態を表示するためのプロパティです。
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

                    // 値が変化したタイミングで、PropertyChanged イベントを呼び、変更をビューに伝えます。
                    if (this.PropertyChanged != null)
                    {
                        this.PropertyChanged(this, new PropertyChangedEventArgs("StatusText"));
                    }
                }
            }
        }

        /// <summary>
        ///  MainPage のシャットダウンの処理です。
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void MainPage_Unloaded(object sender, RoutedEventArgs e)
        {
            if (this.infraredFrameReader != null)
            {
                // InfraredFrameReder オブジェクトを解放します。
                this.infraredFrameReader.Dispose();
                this.infraredFrameReader = null;
            }

            if (this.kinectSensor != null)
            {
                // Kinect センサーの処理を終了します。
                this.kinectSensor.Close();
                this.kinectSensor = null;
            }
        }

        /// <summary>
        /// 赤外線センサーからフレームが到着した際に呼びされるイベントハンドラーです。
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void Reader_InfraredFrameArrived(object sender, InfraredFrameArrivedEventArgs e)
        {
            bool infraredFrameProcessed = false;

            
            using (InfraredFrame infraredFrame = e.FrameReference.AcquireFrame())
            {
                //赤外線フレームが存在するか場合処理を継続します。
                if (infraredFrame != null)
                {
                    //赤外線のフレームに関する情報を含んだオブジェクトを取得します。
                    FrameDescription infraredFrameDescription = infraredFrame.FrameDescription;

                    //infraredFrameDescription が持っている縦横のサイズと到着した赤外線フレームの縦横サイズを比較検証し、
                    //一致していれば次に進みます。
                    if (((infraredFrameDescription.Width * infraredFrameDescription.Height) == this.infraredFrameData.Length) &&
                        (infraredFrameDescription.Width == this.bitmap.PixelWidth) && (infraredFrameDescription.Height == this.bitmap.PixelHeight))
                    {
                        // フレームデータの情報を配列にコピーします。
                        infraredFrame.CopyFrameDataToArray(this.infraredFrameData);
                        infraredFrameProcessed = true;
                    }
                }
            }

            // 赤外線データを元に描画処理を実行します。
            if (infraredFrameProcessed)
            {
                //赤外線データを、RGB 値に変換します。
                this.ConvertInfraredData();

                //変換したデータを画面に描画します。
                this.RenderInfraredPixels(this.infraredPixels);
            }
        }

        /// <summary>
        /// フレーム内の赤外線情報を RGB 値に変換します。
        /// </summary>
        private void ConvertInfraredData()
        {

            int colorPixelIndex = 0;
            for (int i = 0; i < this.infraredFrameData.Length; ++i)
            {
                // 到着した赤外線データを、ピクセル毎に [InfraredOutputValueMinimum, InfraredOutputValueMaximum] 
                // 範囲で指定した範囲に収まるように正規化します。
                // 正規化の手順は、5 つのステップで実行されます。

                // 手順1. infraredFrameData[i] （各ピクセルの輝度が入っている） を、輝度の上限で割ります。
                // 該当のピクセルの輝度の上限に対する割合が得られます。
                float intensityRatio = (float)this.infraredFrameData[i] / InfraredSourceValueMaximum;

                // 手順2. 右の式を計算します。 (1 で求めた比率) / (輝度の平均値 * 標準偏差) 
                intensityRatio /= InfraredSceneValueAverage * InfraredSceneStandardDeviations;

                // 手順3. 2 で求めた値が、InfraredOutputValueMaximum より大きくなっていないか確認します。
                intensityRatio = Math.Min(InfraredOutputValueMaximum, intensityRatio);

                // 手順4. 2 で求めた値が、InfraredOutputValueMinimum より小さくなっていないか確認します。 
                intensityRatio = Math.Max(InfraredOutputValueMinimum, intensityRatio);

                // 上記でも求めた正規化した輝度の値を、RGB 値に変換します。
                byte intensity = (byte)(intensityRatio * 255.0f);
                this.infraredPixels[colorPixelIndex++] = intensity;
                this.infraredPixels[colorPixelIndex++] = intensity;
                this.infraredPixels[colorPixelIndex++] = intensity;
                this.infraredPixels[colorPixelIndex++] = 255;
            }
        }

        /// <summary>
        /// 画面への描画処理を実行します。
        /// </summary>
        /// <param name="pixels">pixel data</param>
        private void RenderInfraredPixels(byte[] pixels)
        {
            pixels.CopyTo(this.bitmap.PixelBuffer);
            this.bitmap.Invalidate();
            theImage.Source = this.bitmap;
        }

        /// <summary>
        ///  Kinect センサーの状態が変わったときに呼び出されます。
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void Sensor_IsAvailableChanged(object sender, IsAvailableChangedEventArgs e)
        {
            this.StatusText = this.kinectSensor.IsAvailable ? resourceLoader.GetString("RunningStatusText")
                                                            : resourceLoader.GetString("SensorNotAvailableStatusText");
        }
    }
}
