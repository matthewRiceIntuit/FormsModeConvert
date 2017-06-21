using Microsoft.Win32;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Xml;
using System.Xml.Linq;

namespace FormsModeDemo
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private const int  kPixelRatio = 10;
        private System.Windows.Forms.WebBrowser browser;
        private MiniCalcPsuedoService srvce = new MiniCalcPsuedoService();

        public MainWindow()
        {
            InitializeComponent();

            //Ensure internal webbrowser is using IE11
            webbrowser_tweak();

            this.Width = 900;
            this.Height = 500;
            browser = wfh.Child as System.Windows.Forms.WebBrowser;
            browser.ObjectForScripting = new ScriptInterface(this);

            string html = File.ReadAllText("index2.html");
            //hack to get images to show in embedded browser.

            html = html.Replace(" src=\"" , string.Format(" src=\"file:{0}", System.AppDomain.CurrentDomain.BaseDirectory.Replace("\\", "/").Replace(" ","%20")));
            browser.DocumentText = html;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
      
            try
            {
                XmlDocument doc = new XmlDocument();
                //doc.Load(@"artifact\fdia7601.ptform.xml");
                doc.Load(@"artifact\fdga0101.ptform.xml");
                XmlDocument mapping = new XmlDocument();
                mapping.Load(@"artifact\fdg_gift_709.xml");
                int rowId = 0;
                int colId = 0;

                string json = "'asset': {'type':'formPage','id':'formPage','values':[";
                foreach (XmlNode row in doc.SelectNodes("formdoc/default_context/page_images/page_image/rows//row"))
                {
                    ++rowId;
                    json += "{'asset':{ 'type':'formRow','id':'r"+rowId+"','values':[";
                    foreach (XmlNode cell in row.SelectNodes("cells/*"))
                    {
                        ++colId;
                        string id = "r" + rowId + "c" + colId;
                        json += render_cell(cell,mapping, id, false);
                    }
                    json += "]}},";
                }
                json += "]}";
                json = json.Replace(",}","}").Replace(",]","]").Replace("'","\"").Replace("&apos;","'");
                 File.WriteAllText("ptform.js",json);

            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
            }
 
        }

        private string render_cell(XmlNode cell, XmlDocument mapping, string id, bool isDLT)
        {
            int width = int.Parse(cell.Attributes["width"].Value) * kPixelRatio;
            if(cell.Name == "cell_run") width *= int.Parse(cell.Attributes["cells"].Value);
            string json = "{'asset':{'type':'formCell','id':'" + id + "','width':" + width;
            if (cell.Name == "cell")
            {
                if (cell.FirstChild != null)
                {
                    XmlNode node;
                    if(null!= cell.SelectSingleNode("attributes")){
                        node = cell.SelectSingleNode("attributes/mid_cell_lines");
                        if(node!=null) json += get_midlines(node);
                        node = cell.SelectSingleNode("attributes/grid_lines");
                        if(node!=null) json += get_gridlines(node);
                    }
                    node = cell.SelectSingleNode("text");
                    if (node!=null)  json += render_text_runs(node, id);
                    node = cell.SelectSingleNode("field_image");
                    if (node!=null) json +=  render_fieldimage(node, mapping);
                    node = cell.SelectSingleNode("array_element_image");
                    if (node!=null && !isDLT) json +=  render_dlt(node,mapping);
                    if (node!=null && isDLT) json +=  render_array_element(node,mapping);
                }
            }
            return json + "}},";
        }

        private string get_midlines(XmlNode lines){
            string result = ", 'midLine':'";
            foreach (XmlNode line in lines.ChildNodes)
            {
                result += line.Name + line.Attributes["origin_and_length"].Value;
            }
            return result.Replace(" ","")+"'";
        }

        private string get_gridlines(XmlNode gridlines){
            string result = ", 'gridLine':'";
            foreach (XmlNode line in gridlines.ChildNodes)
            {
                result+= "line_"+ line.Name;
            }
            return result+"'";
        }

        private string render_text_runs(XmlNode text_node,string id)
        {
            string text = text_node.Attributes["value"].Value.Replace(@"%(CTY)","2016").Replace("\"","'");
            string json = ",'values':[";

            int index=0;
            foreach(XmlNode run in  text_node.SelectNodes("style_run")){
                int width =int.Parse(run.Attributes["width"].Value);
                try
                {
                    json += string.Format("{{'asset':{{'type':'formTextSpan','className':'{0}','value':'{1}'}}}},", run.Attributes["text_style"].Value, text.Substring(index, width).Replace("'", "&apos;"));
                }
                catch
                {
                    json += string.Format("{{'asset':{{'type':'formTextSpan','className':'{0}','value':'{1}'}}}},", run.Attributes["text_style"].Value, text.Substring(index).Replace("'", "&apos;"));
                }
                index += width;
            }
            json += "],";
            if (null != text_node.Attributes["justification"] && text_node.Attributes["justification"].Value != "left") json += string.Format("'justification':'{0}',", text_node.Attributes["justification"].Value);
            if (null != text_node.Attributes["leader"] && text_node.Attributes["leader"].Value != "none" ) json += string.Format("'leader':'{0}',", text_node.Attributes["leader"].Value);
            return json;

        }




        private string render_fieldimage(XmlNode field, XmlDocument mapping)
        {
            try
            {
                XmlNode field_att = field.OwnerDocument.SelectSingleNode("formdoc/default_context/fields/field[@id='" + field.Attributes["id"].Value + "']/field_attributes");
                string field_type = field_att.SelectSingleNode("main/type/@value").Value.ToLower();
                string field_subtype = "";
                if (field_type.Contains(':'))
                {
                    char[] delimiterChars = { ':' };
                    field_subtype = string.Format("', 'subformat':'{0}", field_type.Split(delimiterChars)[1].Substring(1));
                    field_type = field_type.Split(delimiterChars)[0];
                }
                string enterable = (field_att.SelectSingleNode("main/field_bit_attributes/user_enterable") != null).ToString().ToLower();
                string binding = lookup_binding(field.Attributes["id"].Value, mapping);
                string id = binding;
                string dlt_index = "";
                if(field.Attributes["array_id"]!=null){
                    binding = string.Format("{0}[_index_].{1}",field.Attributes["array_id"].Value, binding);
                    dlt_index = ",'dlt_index':'_index_'";
                }
                string justification = field.Attributes["justification"].Value;
                if (justification == "natural") justification += field_type;
                string asset_type = (field_type == "quickzoom") ? "QZ" : ((field_type == "box") ? "Box" : "");
                string set_id = "";
                if (asset_type == "Box")
                {
                    XmlNode fields = field.OwnerDocument.SelectSingleNode("formdoc/default_context/box_sets/box_set/fields[field[@id='" + field.Attributes["id"].Value + "']]");
                    if (fields.ChildNodes.Count > 1)
                    {
                        asset_type = "BoxSet";
                        set_id = ", 'set_id':'" + fields.ParentNode.Attributes["id"].Value + "'";
                        if (fields.ParentNode.Attributes["id"].Value + "N" == field.Attributes["id"].Value)
                        {
                            id += "-FALSE";
                        }
                    }
                }

                string json =  string.Format(",'justification':'{5}', 'text_style':'{6}', 'values':[{{'asset':{{ 'type':'formField{4}','className':'{0}','binding':'{1}','id':'{8}','format':'{2}{7}','enterable':{3} {9} {10} }} }} ] ",
                    field.Attributes["text_style"].Value,
                    binding,
                    field_type,
                    enterable,
                    asset_type,
                    justification,
                    field.Attributes["text_style"].Value,
                    field_subtype,
                    id,
                    set_id,
                    dlt_index
                    );
                return json;
            }
            catch (Exception ex)
            {
                
                Debug.WriteLine(ex.ToString());
                Debug.WriteLine(field.ParentNode.InnerXml);
                return "";
            }
        }

        private string render_array_element(XmlNode field, XmlDocument mapping){
            string dlt_id = field.Attributes["array_id"].Value;
            //((XmlElement)field).SetAttribute("id",string.Format("{0}[_index_].{1}",dlt_id, field.Attributes["field_id"].Value));
            ((XmlElement)field).SetAttribute("id",field.Attributes["field_id"].Value);
            ((XmlElement)field).SetAttribute("justification", field.FirstChild.Attributes["justification"].Value);
            ((XmlElement)field).SetAttribute("text_style", field.FirstChild.Attributes["text_style"].Value);
            return render_fieldimage(field,mapping);
        }

        private string render_dlt(XmlNode field, XmlDocument mapping){
            if(field.Attributes["index"].Value!="1") return "";
            string dlt_id = field.Attributes["array_id"].Value;
            XmlNode dlt_info = field.OwnerDocument.SelectSingleNode("formdoc/default_context/arrays_and_tables/array[@id='"+dlt_id+"']");
            string first_elem_id =dlt_info.SelectSingleNode("main_info/fields/field[1]/@id").Value;
            if(field.Attributes["field_id"].Value != first_elem_id ) return "";
            string last_elem_id = dlt_info.SelectSingleNode("main_info/fields/field[last()]/@id").Value;

            int visible_rows = Int32.Parse(dlt_info.SelectSingleNode("main_info/number_of_visible_elements/@value").Value);
            int page_width = Int32.Parse(field.OwnerDocument.SelectSingleNode("formdoc/form_setup/page_setup/page/width/@value").Value);
            int left_padding = Int32.Parse(dlt_info.SelectSingleNode("dyn_table/header_and_border/left_padding/@value").Value);
            int right_padding = Int32.Parse(dlt_info.SelectSingleNode("dyn_table/header_and_border/right_padding/@value").Value);
            int width = (page_width - left_padding - right_padding) * kPixelRatio;




            string json = ",'values':[{'asset':{'type':'formDLT',  'height':'"+ (visible_rows*19) +"', 'values':[";

            for(int index=1;index<visible_rows+1;++index){
                XmlNode current_cell = field.ParentNode;
                int count=0;
                width=0;
                while(true){
                   string id = string.Format("{0}-{1}-{2}", dlt_id, index, count);
                   width += int.Parse(current_cell.Attributes["width"].Value) * kPixelRatio;
                   json += render_cell(current_cell,mapping,id,true).Replace("_index_",index.ToString());
                   XmlNode field_node_id = current_cell.SelectSingleNode("array_element_image/@field_id");
                   if(field_node_id!=null && last_elem_id == field_node_id.Value) break;
                   current_cell = current_cell.NextSibling;
                   ++count;
                }
            }
            json += "],'width':'"+ width +"'}}]";
            return json;
            
        }


        private string lookup_binding(string tps_id, XmlDocument mapping)
        {
            XmlNode field = mapping.SelectSingleNode("PdfInfoML/common/field[region/@id = '"+tps_id+"[0]']");
            if (field==null) return tps_id;

            return /*mapping.SelectSingleNode("PdfInfoML/form/@bind").Value +"/"*/ "ReturnData.IRS709[0]." + field.Attributes["id"].Value.Replace("/",".");
        }




        #region webbrowser_tweak
        private string InvokeScript(string pram){
            object[] param = new object[1];
            param[0] = pram;
            return browser.Document.InvokeScript("eval", param) as string;
        }

        public static void webbrowser_tweak()
        {
            var appName = Process.GetCurrentProcess().ProcessName + ".exe";
            RegistryKey Regkey = null;
            try
            {
                if (Environment.Is64BitOperatingSystem)
                    Regkey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\\Wow6432Node\\Microsoft\\Internet Explorer\\MAIN\\FeatureControl\\FEATURE_BROWSER_EMULATION", true);
                else
                    Regkey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\\Microsoft\\Internet Explorer\\Main\\FeatureControl\\FEATURE_BROWSER_EMULATION", true);

                if (Regkey == null)
                {
                    MessageBox.Show("webbrowser_tweak - Address Not found");
                    return;
                }

                string FindAppkey = Convert.ToString(Regkey.GetValue(appName));
                if (FindAppkey == "11000")
                {
                    Regkey.Close();
                    return;
                }
                Regkey.SetValue(appName, unchecked((int)0x2AF8), RegistryValueKind.DWord);
                FindAppkey = Convert.ToString(Regkey.GetValue(appName));
            }
            catch (Exception ex)
            {
                MessageBox.Show("webbrowser_tweak: FEATURE_BROWSER_EMULATION Setting Failed");
                MessageBox.Show(ex.Message);
            }
            finally
            {
                if (Regkey != null)
                    Regkey.Close();
            }
        }
        #endregion





        [System.Runtime.InteropServices.ComVisibleAttribute(true)]

        public class ScriptInterface
        {
            MainWindow main;
            public ScriptInterface(Window w)
            {
                this.main = (MainWindow)w;
            }

            public void validate(string binding, string value, string model_json)
            {

               this.main.srvce.update_value(binding, value);
               XDocument model_xml = JsonConvert.DeserializeXNode( model_json);
               XmlDocument model = this.main.srvce.calc(model_xml);
               model.Save("artifact/model_updated.xml");
               string new_model = JsonConvert.SerializeXmlNode(model, Newtonsoft.Json.Formatting.None, true);
               this.main.InvokeScript("FormPlayer.setModel("+new_model+")");

            }
        }

    }
}
