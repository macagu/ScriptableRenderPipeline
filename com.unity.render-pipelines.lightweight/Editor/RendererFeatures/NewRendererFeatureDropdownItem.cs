using UnityEngine.Rendering.LWRP;

namespace UnityEditor.Rendering.LWRP
{
    public static class NewRendererFeatureDropdownItem
    {
        static readonly string defaultNewClassName = "CustomRenderPassFeature.cs";
        static readonly string templateGuid = "51493ed8d97d3c24b94c6cffe834630b";

        [MenuItem("Assets/Create/Rendering/Lightweight Render Pipeline/Renderer Feature", priority = EditorUtils.lwrpAssetCreateMenuPriorityGroup2)]
        public static void CreateNewRendererFeature()
        {
            string templatePath = AssetDatabase.GUIDToAssetPath(templateGuid);
            if (string.IsNullOrEmpty(templatePath))
                templatePath = LightweightRenderPipelineAsset.packagePath + "/Editor/RendererFeatures/NewRendererFeature.cs.txt";

            ProjectWindowUtil.CreateScriptAssetFromTemplateFile(templatePath, defaultNewClassName);
        }
    }
}
