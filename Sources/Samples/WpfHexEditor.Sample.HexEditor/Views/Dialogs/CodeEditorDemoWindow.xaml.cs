//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Windows;

namespace WpfHexEditor.Sample.HexEditor.Views.Dialogs
{
    /// <summary>
    /// Demo window showing CodeEditor with its auto-generated Settings Panel
    /// </summary>
    public partial class CodeEditorDemoWindow : Window
    {
        public CodeEditorDemoWindow()
        {
            InitializeComponent();

            // Connect CodeEditor to Settings Panel
            SettingsPanel.CodeEditorControl = CodeEditorControl;

            // Load sample JSON for demo
            LoadSampleJson();
        }

        private void LoadSampleJson()
        {
            var sampleJson = @"{
  ""formatName"": ""PNG Image"",
  ""version"": ""1.0"",
  ""description"": ""Portable Network Graphics file format"",
  ""extensions"": ["".png""],
  ""author"": ""PNG Group"",
  ""website"": ""http://www.libpng.org/pub/png/"",
  ""detection"": {
    ""signature"": ""89504E470D0A1A0A""
  },
  ""blocks"": [
    {
      ""type"": ""signature"",
      ""name"": ""PNG Signature"",
      ""fields"": [
        {
          ""type"": ""bytes"",
          ""name"": ""Magic"",
          ""length"": 8,
          ""value"": ""89504E470D0A1A0A""
        }
      ]
    },
    {
      ""type"": ""field"",
      ""name"": ""IHDR Chunk"",
      ""fields"": [
        {
          ""type"": ""uint32"",
          ""name"": ""Length"",
          ""endianness"": ""big""
        },
        {
          ""type"": ""ascii"",
          ""name"": ""Type"",
          ""length"": 4,
          ""value"": ""IHDR""
        },
        {
          ""type"": ""uint32"",
          ""name"": ""Width"",
          ""endianness"": ""big""
        },
        {
          ""type"": ""uint32"",
          ""name"": ""Height"",
          ""endianness"": ""big""
        }
      ]
    }
  ]
}";

            CodeEditorControl.LoadText(sampleJson);
        }
    }
}
