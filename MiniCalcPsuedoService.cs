using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace FormsModeDemo
{
    class MiniCalcPsuedoService
    {

        ///  ./MiniCalc --formSource TaxContent-GFT/src/main/resources -m TaxContent-Schema/GiftElementTypes.xml -i JoesBigOne.xml


        /// <summary>
        ///  THIS ENTIRE CLASS IS ONE BIG HACK IN ORDER TO GET SOME KIND OF SIMPLE CALCULATION ENGINE UP AND RUNNNING
        /// </summary>

        public XmlDocument model = new XmlDocument();
        public MiniCalcPsuedoService()
        {
            model.Load("artifact/example.xml");
        }

        public void update_value(string id, string value)
        {
            XmlNode model_field = model.SelectSingleNode(id.Replace("]","+1]"));
            if (model_field == null)
            {
                model_field = model.CreateElement(id);
                model.SelectSingleNode("Return").AppendChild(model_field);
            }
            model_field.InnerText = value;
        }

        public XmlDocument calc(XDocument updated_model)
        {
            //model.Save("artifact/model.xml");
            //File.WriteAllText("artifact/model.xml", update_model);
            //updated_model.Save("artifact/model.xml");
            string result_xml = exec_miniengine("model.xml",
                                                "TaxContent-Schema/GiftElementTypes.xml",
                                                "TaxContent-GFT/src/main/resources");

            XmlDocument result = new XmlDocument();
            result.LoadXml(result_xml);
            if (result.FirstChild.NodeType == XmlNodeType.XmlDeclaration)
                result.RemoveChild(result.FirstChild);
            return result;
        }

        private string exec_miniengine(string model,string meta,string calc){
            
      
            StringBuilder outputBuilder;
            ProcessStartInfo processStartInfo;
            Process process;

            outputBuilder = new StringBuilder();

            processStartInfo = new ProcessStartInfo();
            processStartInfo.CreateNoWindow = true;
            processStartInfo.RedirectStandardOutput = true;
            processStartInfo.RedirectStandardInput = true;
            processStartInfo.UseShellExecute = false;
            processStartInfo.Arguments = string.Format("--formSource artifact/{0} -m artifact/{1} -i artifact/{2}", calc, meta, model);
            processStartInfo.FileName = "artifact/MiniCalc.exe";

            process = new Process();
            process.StartInfo = processStartInfo;
            process.EnableRaisingEvents = true;
            process.OutputDataReceived += new DataReceivedEventHandler
            (
                delegate(object sender, DataReceivedEventArgs e)
                {
                    outputBuilder.Append(e.Data + "\n");
                }
            );
            process.Start();
            process.BeginOutputReadLine();
            process.WaitForExit();
            process.CancelOutputRead();
            return  outputBuilder.ToString();
        }

    }
}
