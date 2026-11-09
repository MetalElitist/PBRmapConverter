
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

public class PBRMapConverterWindow : EditorWindow
{
	public enum Channel
	{
		R, G, B, A
	}

	[System.Serializable]
	public class InputMap
	{
		public string name = "Input 1";
		public Texture2D texture;
		public float value = 1f;
	}

	
	[System.Serializable]
	public class OutputChannel
	{
		public bool useConstant = false;
		public float constantValue = 1f;

		public int inputIndex = 0;
		public Channel sourceChannel = Channel.R;
		public bool invert = false;
	}


	[SerializeField]
	List<InputMap> inputs = new List<InputMap>();

	[SerializeField]
	OutputChannel outputR = new OutputChannel();

	[SerializeField]
	OutputChannel outputG = new OutputChannel();

	[SerializeField]
	OutputChannel outputB = new OutputChannel();

	[SerializeField]
	OutputChannel outputA = new OutputChannel();

	// Temporary readable copies for textures that do not have Read/Write enabled.
	Dictionary<Texture2D, Texture2D> readableTextureCache;

	[SerializeField]
	int width = 2048;

	[SerializeField]
	int height = 2048;

	[MenuItem("Tools/PBR Map Converter")]
	static void OpenWindow()
	{
		GetWindow<PBRMapConverterWindow>(
			"PBR Map Converter"
		);
	}

	void OnEnable()
	{
		if (inputs == null)
			inputs = new List<InputMap>();

		if (inputs.Count == 0)
		{
			inputs.Add(new InputMap());
		}
	}

	void OnGUI()
	{
		EditorGUILayout.Space(10);

		GUILayout.Label(
			"PBR MAP CONVERTER 2.0",
			EditorStyles.boldLabel
		);

		EditorGUILayout.HelpBox(
			"Add input textures or use constant values. " +
			"Connect any input to any output channel.",
			MessageType.Info
		);

		EditorGUILayout.Space(10);

		DrawInputs();

		EditorGUILayout.Space(15);

		GUILayout.Label(
			"OUTPUT CHANNELS",
			EditorStyles.boldLabel
		);

		DrawOutput("Red (R)", outputR);
		DrawOutput("Green (G)", outputG);
		DrawOutput("Blue (B)", outputB);
		DrawOutput("Alpha (A)", outputA);

		EditorGUILayout.Space(15);

		GUILayout.Label(
			"EXPORT SETTINGS",
			EditorStyles.boldLabel
		);

		width = EditorGUILayout.IntField(
			"Width",
			width
		);

		height = EditorGUILayout.IntField(
			"Height",
			height
		);

		width = Mathf.Clamp(width, 2, 8192);
		height = Mathf.Clamp(height, 2, 8192);

		EditorGUILayout.Space(15);

		GUI.backgroundColor = new Color(
			0.4f, 0.8f, 0.5f
		);

		if (GUILayout.Button(
			"GENERATE TEXTURE",
			GUILayout.Height(40)))
		{
			ExportTexture();
		}

		GUI.backgroundColor = Color.white;
	}

	void DrawInputs()
	{
		EditorGUILayout.BeginHorizontal();

		GUILayout.Label(
			"INPUT MAPS",
			EditorStyles.boldLabel
		);

		GUILayout.FlexibleSpace();

		if (GUILayout.Button("+", GUILayout.Width(30)))
		{
			inputs.Add(new InputMap
			{
				name = "Input " + (inputs.Count + 1),
				value = 1f
			});
		}

		EditorGUILayout.EndHorizontal();

		for (int i = 0; i < inputs.Count; i++)
		{
			InputMap input = inputs[i];

			EditorGUILayout.BeginVertical(
				EditorStyles.helpBox
			);

			EditorGUILayout.BeginHorizontal();

			input.name = EditorGUILayout.TextField(
				input.name,
				EditorStyles.boldLabel
			);

			if (GUILayout.Button(
				"X",
				GUILayout.Width(25)))
			{
				inputs.RemoveAt(i);

				if (inputs.Count == 0)
					inputs.Add(new InputMap());

				ClampOutputIndices();

				EditorGUILayout.EndHorizontal();
				EditorGUILayout.EndVertical();

				break;
			}

			EditorGUILayout.EndHorizontal();

			Texture2D previousTexture = input.texture;

			input.texture = (Texture2D)EditorGUILayout.ObjectField(
				"Texture",
				input.texture,
				typeof(Texture2D),
				false
			);

			if (input.texture != null && input.texture != previousTexture)
			{
				if (inputs.IndexOf(input) == 0)
				{
					width = input.texture.width;
					height = input.texture.height;
				}
			}

			EditorGUILayout.EndVertical();
		}
	}

	
	void DrawOutput(
		string title,
		OutputChannel output)
	{
		EditorGUILayout.BeginVertical(
			EditorStyles.helpBox
		);

		GUILayout.Label(
			title,
			EditorStyles.boldLabel
		);

		output.useConstant = EditorGUILayout.Toggle(
			"Use Constant",
			output.useConstant
		);

		if (output.useConstant)
		{
			output.constantValue = EditorGUILayout.Slider(
				"Constant Value",
				output.constantValue,
				0f,
				1f
			);
		}
		else
		{
			string[] inputNames = new string[inputs.Count];

			for (int i = 0; i < inputs.Count; i++)
			{
				inputNames[i] = inputs[i].name;

				if (string.IsNullOrWhiteSpace(inputNames[i]))
					inputNames[i] = "Input " + (i + 1);
			}

			output.inputIndex = Mathf.Clamp(
				output.inputIndex,
				0,
				inputs.Count - 1
			);

			output.inputIndex = EditorGUILayout.Popup(
				"Input Source",
				output.inputIndex,
				inputNames
			);

			output.sourceChannel = (Channel)
				EditorGUILayout.EnumPopup(
					"Source Channel",
					output.sourceChannel
				);
		}

		output.invert = EditorGUILayout.Toggle(
			"Invert",
			output.invert
		);

		EditorGUILayout.EndVertical();
	}


	void ClampOutputIndices()
	{
		if (inputs.Count == 0)
			return;

		outputR.inputIndex = Mathf.Clamp(
			outputR.inputIndex, 0, inputs.Count - 1);

		outputG.inputIndex = Mathf.Clamp(
			outputG.inputIndex, 0, inputs.Count - 1);

		outputB.inputIndex = Mathf.Clamp(
			outputB.inputIndex, 0, inputs.Count - 1);

		outputA.inputIndex = Mathf.Clamp(
			outputA.inputIndex, 0, inputs.Count - 1);
	}

	float GetChannelValue(
		Color pixel,
		Channel channel)
	{
		switch (channel)
		{
			case Channel.R: return pixel.r;
			case Channel.G: return pixel.g;
			case Channel.B: return pixel.b;
			case Channel.A: return pixel.a;
		}

		return 0f;
	}

	
	float GetValue(
		OutputChannel output,
		float u,
		float v)
	{
		float value;

		if (output.useConstant)
		{
			value = output.constantValue;
		}
		else
		{
			InputMap input = inputs[
				Mathf.Clamp(
					output.inputIndex,
					0,
					inputs.Count - 1
				)
			];

			if (input.texture != null)
			{
				Texture2D sourceTexture = input.texture;

				if (!sourceTexture.isReadable &&
					readableTextureCache != null &&
					readableTextureCache.TryGetValue(sourceTexture, out Texture2D readableCopy))
				{
					sourceTexture = readableCopy;
				}

				Color pixel = sourceTexture.GetPixelBilinear(u, v);

				value = GetChannelValue(
					pixel,
					output.sourceChannel
				);
			}
			else
			{
				value = input.value;
			}
		}

		if (output.invert)
			value = 1f - value;

		return Mathf.Clamp01(value);
	}


	string GetDefaultFolder()
	{
		foreach (InputMap input in inputs)
		{
			if (input.texture == null)
				continue;

			string assetPath =
				AssetDatabase.GetAssetPath(input.texture);

			if (string.IsNullOrEmpty(assetPath))
				continue;

			string folder =
				Path.GetDirectoryName(assetPath);

			if (!string.IsNullOrEmpty(folder))
			{
				return Path.GetFullPath(folder);
			}
		}

		return Path.GetFullPath("Assets");
	}

	
Texture2D CreateReadableCopy(Texture2D source)
{
    RenderTexture renderTexture = RenderTexture.GetTemporary(
        source.width,
        source.height,
        0,
        RenderTextureFormat.ARGB32,
        RenderTextureReadWrite.Linear
    );

    RenderTexture previousActive = RenderTexture.active;
    Texture2D readableCopy = null;

    try
    {
        Graphics.Blit(source, renderTexture);
        RenderTexture.active = renderTexture;

        readableCopy = new Texture2D(
            source.width,
            source.height,
            TextureFormat.RGBA32,
            false,
            true
        );

        readableCopy.ReadPixels(
            new Rect(0, 0, source.width, source.height),
            0,
            0
        );
        readableCopy.Apply(false, false);

        readableCopy.filterMode = source.filterMode;
        readableCopy.wrapMode = source.wrapMode;
        readableCopy.anisoLevel = source.anisoLevel;

        return readableCopy;
    }
    catch
    {
        if (readableCopy != null)
            DestroyImmediate(readableCopy);

        throw;
    }
    finally
    {
        RenderTexture.active = previousActive;
        RenderTexture.ReleaseTemporary(renderTexture);
    }
}

void ExportTexture()
{
    if (inputs.Count == 0)
        return;

    // Open save dialog in the actual source folder
    string defaultFolder = GetDefaultFolder();

    string absolutePath = EditorUtility.SaveFilePanel(
        "Save PBR Texture",
        defaultFolder,
        "PBR_Output",
        "png"
    );

    if (string.IsNullOrEmpty(absolutePath))
        return;

    Texture2D result = new Texture2D(
	    width,
	    height,
	    TextureFormat.RGBA32,
	    false,
	    true
    );

    try
    {
        readableTextureCache = new Dictionary<Texture2D, Texture2D>();

        foreach (InputMap input in inputs)
        {
            if (input.texture == null || input.texture.isReadable)
                continue;

            if (!readableTextureCache.ContainsKey(input.texture))
                readableTextureCache.Add(
                    input.texture,
                    CreateReadableCopy(input.texture)
                );
        }

	    Color[] pixels = new Color[
		    width * height
	    ];

	    for (int y = 0; y < height; y++)
	    {
		    float v = (float)y / (height - 1);

		    for (int x = 0; x < width; x++)
		    {
			    float u = (float)x / (width - 1);

			    int index = y * width + x;

			    pixels[index] = new Color(
				    GetValue(outputR, u, v),
				    GetValue(outputG, u, v),
				    GetValue(outputB, u, v),
				    GetValue(outputA, u, v)
			    );
		    }
	    }

        result.SetPixels(pixels);
        result.Apply(false, false);

        byte[] pngData = result.EncodeToPNG();

        File.WriteAllBytes(absolutePath, pngData);

        // Check whether the file is inside Unity Assets
        string assetsPath = Path.GetFullPath(
            Application.dataPath
        ).TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar
        );

        string normalizedPath = Path.GetFullPath(
            absolutePath
        );

        string assetPath = null;

        string assetsPrefix =
            assetsPath + Path.DirectorySeparatorChar;

        if (normalizedPath.StartsWith(
            assetsPrefix,
            System.StringComparison.OrdinalIgnoreCase))
        {
            assetPath = "Assets" +
                normalizedPath.Substring(assetsPath.Length)
                .Replace("\\", "/");
        }

        // Import only if saved inside Unity project
        if (!string.IsNullOrEmpty(assetPath))
        {
            AssetDatabase.ImportAsset(
                assetPath,
                ImportAssetOptions.ForceUpdate
            );

            TextureImporter importer =
                AssetImporter.GetAtPath(assetPath)
                as TextureImporter;

            if (importer != null)
            {
                importer.textureType =
                    TextureImporterType.Default;

                importer.sRGBTexture = false;

                importer.alphaSource =
                    TextureImporterAlphaSource.FromInput;

                importer.alphaIsTransparency = false;

                importer.mipmapEnabled = false;

                importer.textureCompression =
                    TextureImporterCompression.Uncompressed;

                importer.SaveAndReimport();
            }
        }

        Debug.Log(
            "PBR texture exported: " + absolutePath
        );

        EditorUtility.DisplayDialog(
            "Success",
            "Texture generated successfully!\n\n" +
            "RGBA channels preserved.\n\n" +
            absolutePath,
            "OK"
        );
    }
    catch (System.Exception e)
    {
        Debug.LogException(e);

        EditorUtility.DisplayDialog(
            "Export Error",
            e.Message,
            "OK"
        );
    }
    finally
    {
        if (readableTextureCache != null)
        {
            foreach (Texture2D readableCopy in readableTextureCache.Values)
            {
                if (readableCopy != null)
                    DestroyImmediate(readableCopy);
            }

            readableTextureCache = null;
        }

        DestroyImmediate(result);
    }
}

}
