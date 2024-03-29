﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Configuration;
using System.IO;
using Excel = Microsoft.Office.Interop.Excel;

namespace TubesSC
{
    public partial class Form1 : Form
    {
        private ANN<string> ANN = null;

        private Dictionary<string, double[]> TrainingSet = null;
        private int imgHeight = 0;
        private int imgWidth = 0;
        private int NumOfImages = 0;
        
        //for PCA
        double[,] matrixR = new double[1000,1000];
        double[,] matrixF = new double[1000, 1000];

        private delegate bool TrainingCallBack();
        private AsyncCallback asyCallBack = null;
        private IAsyncResult res = null;
        private ManualResetEvent ManualReset = null;

        private DateTime ProcessTime;

        public Form1()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e) //Open Dataset for Training
        {
            FolderBrowserDialog f = new FolderBrowserDialog();
            
            //f.ShowDialog();
            if (f.ShowDialog() == DialogResult.OK)
            {
                filenameTxt.Text = f.SelectedPath;
            }
        }

        void ANN_IterationChanged(object o, NeuralEventArgs args)
        {
            UpdateError(args.CurrentError);
            UpdateIteration(args.CurrentIteration);

            if (ManualReset.WaitOne(0, true))
                args.Stop = true;
        }   
        #region Initialize Dataset Training
        private void InitializeSettings()
        {
            statusTxt.AppendText("Initializing Settings..");

            try
            {
                string[] Images = Directory.GetFiles(filenameTxt.Text, "*.bmp");
                NumOfImages = Images.Length;

                imgHeight = 0;
                imgWidth = 0;

                foreach (string s in Images)
                {
                    Bitmap Temp = new Bitmap(s);
                    imgHeight += Temp.Height;
                    imgWidth += Temp.Width;
                    Temp.Dispose();
                }
                imgHeight /= NumOfImages;
                imgWidth /= NumOfImages;

                int networkInput = imgHeight * imgWidth;

                //inputTxt.Text = /*((int)(double)networkInput).ToString();*/((int)((double)(networkInput + NumOfImages) * .11)).ToString();
                inputTxt.Text = networkInput.ToString();
                hiddenTxt.Text =((int)((double)(networkInput + NumOfImages) * .33)).ToString();
                outputTxt.Text = /*"36";*/NumOfImages.ToString();


            }
            catch (Exception ex)
            {
                MessageBox.Show("Error Initializing Settings: " + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            statusTxt.AppendText("Done!\r\n");
        }
        #endregion

        #region Generate Training Set
        private void GenerateTrainingSet()
        {
            statusTxt.AppendText("Generating Training Set..");

            string[] Images = Directory.GetFiles(filenameTxt.Text, "*.bmp");

            TrainingSet = new Dictionary<string, double[]>(Images.Length);
            foreach (string s in Images)
            {
                Bitmap Temp = new Bitmap(s);
                TrainingSet.Add(Path.GetFileNameWithoutExtension(s), ImageProcessing.ToMatrix(Temp, imgHeight, imgWidth));
                Temp.Dispose();
            }

            statusTxt.AppendText("Done!\r\n");
        }
        #endregion


        #region CreateANN
        private void CreateANN()
        {
            if (TrainingSet == null)
                throw new Exception("Generate Training Set First");

            
            
                //int InputNum = Int16.Parse(inputTxt.Text);
                //int HiddenNum = Int16.Parse(hiddenTxt.Text);

                //ANN = new ANN<string>(new ANNProcess<string>(imgHeight * imgWidth, InputNum, HiddenNum, NumOfImages), TrainingSet);

                int HiddenNum = Int16.Parse(hiddenTxt.Text);

                ANN = new ANN<string>(new ANNProcess2<string>(imgHeight*imgWidth, HiddenNum, NumOfImages), TrainingSet);
            
            

            ANN.IterationChanged += new ANN<string>.IterationChangedCallBack(ANN_IterationChanged);

            ANN.MaximumError = Double.Parse(MSETxt.Text);
        }
        #endregion
        private void Form1_Load(object sender, EventArgs e)
        {

        }

        private void button2_Click(object sender, EventArgs e) //Initialize Settings
        {
            if (filenameTxt.Text == "" || MSETxt.Text == "")
            {
                MessageBox.Show("Select Training Dataset First and Set Minimal Error","Incomplete Procedure Detected",MessageBoxButtons.OK);
            }
            else
            {
                InitializeSettings();

                GenerateTrainingSet();
                CreateANN();

                asyCallBack = new AsyncCallback(TrainingCompleted);
                ManualReset = new ManualResetEvent(false);
            }
        }

        private void TrainingCompleted(IAsyncResult result)
        {
            if (result.AsyncState is TrainingCallBack)
            {
                TrainingCallBack TR = (TrainingCallBack)result.AsyncState;

                bool isSuccess = TR.EndInvoke(res);
                if (isSuccess)
                    UpdateState("Completed Training Process Successfully\r\n");
                else
                    UpdateState("Training Process is Aborted or Exceed Maximum Iteration\r\n");

                SetButtons(true);
                timer1.Stop();
            }
        }
        private void button3_Click(object sender, EventArgs e) //Training button
        {
            UpdateState("Training.....\r\n");
            SetButtons(false);
            ManualReset.Reset();

            TrainingCallBack TR = new TrainingCallBack(ANN.Train);
            res = TR.BeginInvoke(asyCallBack, TR);
            ProcessTime = DateTime.Now;
            timer1.Start();
        }

        private delegate void UpdateUI(object o);
        private void SetButtons(object o)
        {
            
            if (stopBtn.InvokeRequired)
            {
                stopBtn.Invoke(new UpdateUI(SetButtons), o);
            }
            else
            {
                bool b = (bool)o;
                stopBtn.Enabled = !b;
                recognizeBtn.Enabled = b;
                trainBtn.Enabled = b;
                
            }
        }
        private void UpdateError(object o)
        {
            if (errorLbl.InvokeRequired)
            {
                errorLbl.Invoke(new UpdateUI(UpdateError), o);
            }
            else
            {
                errorLbl.Text = "Error: " + ((double)o).ToString(".###");
            }
        }
        private void UpdateIteration(object o)
        {
            if (iterationLbl.InvokeRequired)
            {
                iterationLbl.Invoke(new UpdateUI(UpdateIteration), o);
            }
            else
            {
                iterationLbl.Text = "Epoch: " + ((int)o).ToString();
            }
        }

        private void UpdateState(object o)
        {
            if (statusTxt.InvokeRequired)
            {
                statusTxt.Invoke(new UpdateUI(UpdateState), o);
            }
            else
            {
                statusTxt.AppendText((string)o);
            }
        }

        private void UpdateTimer(object o)
        {
            if (timerLbl.InvokeRequired)
            {
                timerLbl.Invoke(new UpdateUI(UpdateTimer), o);
            }
            else
            {
                timerLbl.Text = (string)o;
            }
        }

        private void stopBtn_Click(object sender, EventArgs e)
        {
            ManualReset.Set();
        }

        private void recognizeBtn_Click(object sender, EventArgs e)
        {
            string MatchedHigh = "?", MatchedLow = "?";
            double OutputValueHight = 0, OutputValueLow = 0;
            Bitmap bmp = new Bitmap(previewPB.Image);
            double[] input = ImageProcessing.ToMatrix(bmp,
                imgHeight, imgWidth);

            ANN.Recognize(input, ref MatchedHigh, ref OutputValueHight,
                ref MatchedLow, ref OutputValueLow);

            ShowRecognitionResults(MatchedHigh, MatchedLow, OutputValueHight, OutputValueLow);

        }

        private void ShowRecognitionResults(string MatchedHigh, string MatchedLow, double OutputValueHight, double OutputValueLow)
        {
            highestValueLbl.Text = "Hight: " + MatchedHigh + " (%" + ((int)100 * OutputValueHight).ToString("##") + ")";
            lowestValueLbl.Text = "Low: " + MatchedLow + " (%" + ((int)100 * OutputValueLow).ToString("##") + ")";

            //pictureBoxInput.Image = new Bitmap(drawingPanel1.ImageOnPanel,
            //    pictureBoxInput.Width, pictureBoxInput.Height);

            if (MatchedHigh != "?")
                HighestMatchedPB.Image = new Bitmap(new Bitmap(filenameTxt.Text + "\\" + MatchedHigh + ".bmp"),
                    HighestMatchedPB.Width, HighestMatchedPB.Height);

            if (MatchedLow != "?")
                LowestMatchedPB.Image = new Bitmap(new Bitmap(filenameTxt.Text + "\\" + MatchedLow + ".bmp"),
                    LowestMatchedPB.Width, LowestMatchedPB.Height);
        }

        private void openTestImageBtn_Click(object sender, EventArgs e)
        {
            OpenFileDialog FD = new OpenFileDialog();
            FD.Filter = "Bitmap Image(*.bmp)|*.bmp";
            FD.InitialDirectory = filenameTxt.Text;
            if (FD.InitialDirectory == "")
            {
                MessageBox.Show("Train First", "Incomplete Procedure", MessageBoxButtons.OK);
            }
            else
            {
                if (FD.ShowDialog() == DialogResult.OK)
                {
                    string FileName = FD.FileName;
                    if (Path.GetExtension(FileName) == ".bmp")
                    {
                        testImageTxt.Text = FileName;
                        previewPB.Image = new Bitmap(FileName);
                    }
                }
                FD.Dispose();
            }
        }

        private void button3_Click_1(object sender, EventArgs e)
        {
            statusTxt.AppendText("Saving Settings..");

            string[] Images = Directory.GetFiles(filenameTxt.Text, "*.bmp");
            NumOfImages = Images.Length;

            imgHeight = 0;
            imgWidth = 0;

            foreach (string s in Images)
            {
                Bitmap Temp = new Bitmap(s);
                imgHeight+= Temp.Height;
                imgWidth += Temp.Width;
                Temp.Dispose();
            }
            imgHeight/= NumOfImages;
            imgWidth /= NumOfImages;

            int networkInput = imgHeight* imgWidth;

            
            outputTxt.Text = NumOfImages.ToString();


            recognizeBtn.Enabled = false;

            statusTxt.AppendText("Done!\r\n");

            GenerateTrainingSet();
            CreateANN();
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            TimeSpan ElapsedTime = DateTime.Now.Subtract(ProcessTime);
            UpdateTimer(ElapsedTime.Hours.ToString("D2") + ":" + ElapsedTime.Minutes.ToString("D2") + ":" + ElapsedTime.Seconds.ToString("D2"));
        }

        private void button3_Click_2(object sender, EventArgs e) //PCA
        {
            string[] Images = Directory.GetFiles(filenameTxt.Text, "*.bmp");
            NumOfImages = Images.Length;
            ImageProcessing ip = new ImageProcessing();


            int x = 0;
            int y = 0;
            foreach (string s in Images)
            {
                Bitmap Temp = new Bitmap(s);
                for (int i = 0; i < Temp.Height; i++)
                {
                    for(int j=0;j<Temp.Width ;j++)
                    {
                        Color c = Temp.GetPixel(i,j);
                        double rgb =(c.R * .3 + c.G * .59 + c.B * .11) / 255;
                        matrixR[x, y] = rgb;
                        y++;
                    }
                    
                }
                y = 0;
                x++;

                Temp.Dispose();
            }
            matrixF = ip.PCA(matrixR);
            SaveToExcel(matrixF);
            MessageBox.Show("Penyimpanan Feature Selesai", "Done", MessageBoxButtons.OK);
        }

        private void SaveToExcel(double[,] matrixR) 
        {
            string projectPath = Path.GetDirectoryName(Path.GetDirectoryName(System.IO.Directory.GetCurrentDirectory()));
            string path = projectPath + @"/Feature/Feature.xlsx";
            Excel.Application ExcelApp = new Microsoft.Office.Interop.Excel.Application();

            Excel.Workbook wb = ExcelApp.Workbooks.Open(path);
            Excel.Worksheet sh = (Excel.Worksheet)wb.Sheets["Sheet1"];

            for (int i = 1; i <= matrixR.GetLength(0); i++)
            {
                for (int j = 1; j <= matrixR.GetLength(1); j++)
                {
                    sh.Cells[i, j].Value = matrixR[i-1, j-1];
                }
            }
            wb.Save();
            wb.Close();
        }
            

        private void button4_Click(object sender, EventArgs e)
        {
            UpdateState("Validating.....\r\n");
            SetButtons(false);
            ManualReset.Reset();

            TrainingCallBack TR = new TrainingCallBack(ANN.Validate);
            res = TR.BeginInvoke(asyCallBack, TR);
            ProcessTime = DateTime.Now;
            timer1.Start();
        }

        private void button5_Click(object sender, EventArgs e)
        {
            string[] Images = Directory.GetFiles(filenameTxt.Text, "*.bmp");
            ImageProcessing ip = new ImageProcessing();
            string savepath = "F:/face/";
            int i = 1;
            foreach (string s in Images)
            {
                Bitmap Temp = new Bitmap(s);
                Bitmap b = ip.BinaryImage(Temp);

                b.Save(savepath + i+".bmp", ImageFormat.Bmp);
                i++;
                Temp.Dispose();
            }
        }
    }
}
