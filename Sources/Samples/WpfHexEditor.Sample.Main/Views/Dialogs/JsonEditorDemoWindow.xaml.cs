// Apache 2.0  2026
// JSON Editor Demo Window - Shows JsonEditor with Settings Panel
// Author: Claude Sonnet 4.5

using System.Windows;

namespace WpfHexEditor.Sample.Main.Views.Dialogs
{
    /// <summary>
    /// Demo window showing JsonEditor with its auto-generated Settings Panel
    /// </summary>
    public partial class JsonEditorDemoWindow : Window
    {
        public JsonEditorDemoWindow()
        {
            InitializeComponent();

            // Connect JsonEditor to Settings Panel
            SettingsPanel.JsonEditorControl = JsonEditorControl;

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

            JsonEditorControl.LoadText(sampleJson);
        }
    }
}
