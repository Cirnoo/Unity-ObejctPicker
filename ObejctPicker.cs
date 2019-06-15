using UnityEngine;
using System.Collections;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System;
using System.Reflection;

public partial class ObejctPicker : EditorWindow
{
	public enum ObjectType
	{
		None,
		AnimationClip,
		AudioClip,
		Material,
		Model,
		Prefab,
		Sprite,
		Texture,
	}

	protected enum InputState
	{
		Input,
		EndInput,
	}
	public class InputBuf
	{
		public string input = string.Empty;
		public int start_index = 0;
		public int select_index = -1;
	}

	public int select_index = -1;
	public int start_index = 0;

	public string input = "";
	protected string[] search_ans = new string[0];
	protected string[] search_folder = null;

	protected InputState inputState = InputState.EndInput;
	protected ObjectType objectType = ObjectType.None;
	protected static float base_height = 420;
	protected static float base_width = 450;
	protected const float edge = 10;
	protected float item_width;
	protected float item_height = 15;
	protected GUIStyle resultsLabel = "PR Label";
	protected GUIStyle toolbarBack = "ObjectPickerToolbar";
	protected short item_num_limit = 25;
	protected Action<ObejctPicker> callBack = null;
	protected bool need_show_path = true;

	private int timer = 0;
	private const int delta_time = 20;

	const int PATH_VIEW_HEIGHT = 30;


	public int End_index
	{
		get { return start_index + item_num_limit; }
	}

	public static ObejctPicker Create(Action<ObejctPicker> callBack = null, ObjectType objectType = ObjectType.None, string[] search_folder = null, InputBuf buf = null)
	{
		return Create<ObejctPicker>(callBack, objectType, search_folder, buf);
	}

	protected static T Create<T>(Action<ObejctPicker> callBack = null, ObjectType objectType = ObjectType.None, string[] search_folder = null, InputBuf buf = null) where T : ObejctPicker
	{
		string type = objectType == ObjectType.None ? "" : objectType.ToString();
		var window = GetWindowWithRect<T>(new Rect(0, 0, base_width, base_height + PATH_VIEW_HEIGHT), true, "Select " + type);
		window.InitData(objectType, search_folder, buf);
		window.callBack = callBack;
		window.DoSearch();
		return window;
	}
	protected virtual void InitData(ObjectType objectType, string[] search_folder = null, InputBuf buf = null)
	{
		item_width = base_width;
		if (buf == null)
			buf = new InputBuf();
		this.objectType = objectType;
		this.search_folder = search_folder;
		input = buf.input;
		select_index = buf.select_index;
		SetStartIndex(buf.start_index);
	}

	private void OnGUI()
	{
		CheckKeyDown();
		DrawSearchArea();
		if (need_show_path)
		{
			DrawPathView();
		}
	}


	void DrawPathView()
	{
		Rect view_rect = new Rect(0f, position.height - PATH_VIEW_HEIGHT, position.width, PATH_VIEW_HEIGHT);
		GUI.Box(view_rect, string.Empty, "PopupCurveSwatchBackground");
		var path = GetSelectedAssetPath();
		GUI.Label(new Rect(view_rect.x + edge, view_rect.y + PATH_VIEW_HEIGHT * 0.2f, position.width - 2 * edge, view_rect.height), path);
	}

	private void DrawSearchArea()
	{
		GUILayout.BeginVertical();
		GUILayoutUtility.GetRect(position.width, 18);
		GUI.Label(new Rect(0f, 0f, position.width, 44), GUIContent.none, toolbarBack);
		GUI.SetNextControlName("SearchFilter");
		var pre_input = DrawSearchField(new Rect(5f, 5f, position.width - 10f, 15f), input);
		if (pre_input != input)
		{
			input = pre_input;
			inputState = InputState.Input;
			timer = 0;
			Repaint();
		}
		else
		{
			EditorGUI.FocusTextInControl("SearchFilter");
		}

		DrawMatchingResult();
		GUILayout.EndVertical();

	}

	public static string DrawSearchField(Rect position, string text)
	{
		Rect position_input = position;
		position_input.width -= 15f;
		text = EditorGUI.TextField(position_input, text, new GUIStyle("SearchTextField"));
		Rect position_cancel = position;
		position_cancel.x += position.width - 15f;
		position_cancel.width = 15f;
		if (GUI.Button(position_cancel, GUIContent.none, string.IsNullOrEmpty(text) ? "SearchCancelButtonEmpty" : "SearchCancelButton"))
		{
			text = string.Empty;
			GUIUtility.keyboardControl = 0;
		}
		return text;
	}

	public virtual string GetSelectedAssetPath()
	{
		var path = GetAssetPathByIndex(select_index);
		if (!string.IsNullOrEmpty(path))
			return Path.GetDirectoryName(GetAssetPathByIndex(select_index));
		else
			return string.Empty;
	}

	protected string GetAssetPathByIndex(int index)
	{
		if (CheckIndexInSearchBuf(index))
			return AssetDatabase.GUIDToAssetPath(search_ans[index]);
		else
			return string.Empty;
	}


	protected virtual string GetAssetNameByIndex(int index)
	{
		return Path.GetFileNameWithoutExtension(GetAssetPathByIndex(index));
	}

	public virtual T GetSelectedObject<T>() where T : UnityEngine.Object
	{
		if (CheckIndexInSearchBuf(select_index))
		{
			return AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(search_ans[select_index]));
		}
		return null;
	}
	private void Update()
	{
		++timer;
		if (inputState == InputState.Input && timer > delta_time)
		{
			DoSearch();
			inputState = InputState.EndInput;
		}
	}

	private void CheckKeyDown()
	{
		var cur_event = Event.current;
		if (cur_event.type == EventType.KeyDown)
		{
			switch (cur_event.keyCode)
			{
				case KeyCode.UpArrow:
					OnIndexChange(-1);
					break;
				case KeyCode.DownArrow:
					OnIndexChange(1);
					break;
				case KeyCode.Return:
					OnObjectSelect();
					Close();
					break;
				case KeyCode.Escape:
					Close();
					break;
				default:
					return;
			}
			Event.current.Use();
		}
		if (cur_event.type == EventType.ScrollWheel)
		{
			OnScrollWheel(cur_event.delta.y);
		}
	}

	protected virtual void OnScrollWheel(float value)
	{
		OnStartIndexChange((int)value);
	}

	protected virtual void OnIndexChange(int value)
	{
		select_index = Clamp(select_index + value, -1, GetSearchAnsLength() - 1);
		if (select_index == End_index)
			OnStartIndexChange(1);
		else if (select_index == start_index && select_index > 0)
			OnStartIndexChange(-1);
	}

	public static int Clamp(int value, int min, int max)
	{
		return (value < min) ? min : (value > max) ? max : value;
	}

	void OnStartIndexChange(int value)
	{
		SetStartIndex(start_index + value);
	}

	void SetStartIndex(int value)
	{
		start_index = Clamp(value, 0, Mathf.Max(0, GetSearchAnsLength() - 1 - item_num_limit));
		Repaint();
	}

	protected virtual void DrawMatchingResult()
	{
		GUILayout.BeginVertical();
		GUILayout.Space(edge);
		var cur_event = Event.current;
		start_index = Mathf.Max(0, start_index);
		var lenth = GetSearchAnsLength();
		for (int i = start_index - 1; i < End_index && i < lenth; i++)
		{
			var name = i == -1 ? "None" : GetAssetNameByIndex(i);

			var elementRect = GUILayoutUtility.GetRect(0, 0, GUILayout.Width(item_width), GUILayout.Height(item_height));

			if (cur_event.type == EventType.Repaint)
			{
				resultsLabel.Draw(elementRect, name, false, false, i == select_index, true);
			}

			if (cur_event.type == EventType.MouseDown && elementRect.Contains(cur_event.mousePosition))
			{
				cur_event.Use();
				select_index = i;
				if (cur_event.clickCount == 2)
				{
					Close();
					return;
				}
				else
				{
					Repaint();
				}
				OnObjectSelect();
			}
		}
		GUILayout.EndVertical();
	}

	protected virtual int GetSearchAnsLength()
	{
		return search_ans.Length;
	}
	protected virtual void OnObjectSelect()
	{
		callBack?.Invoke(this);
	}

	protected void DoSearch()
	{
		SearchByFilter(GetSearchFilterString(), search_folder);
		SetStartIndex(0);
		select_index = -1;
		Repaint();
	}

	protected virtual void SearchByFilter(string filter, string[] folder)
	{
		search_ans = AssetDatabase.FindAssets(GetSearchFilterString(), folder).Distinct().ToArray();
	}

	protected string GetSearchFilterString()
	{
		string type = objectType == ObjectType.None ? "" : " t:" + objectType.ToString();
		return input + type;
	}

	protected bool CheckIndexInSearchBuf(int index)
	{
		return index >= 0 && index < GetSearchAnsLength();
	}
}

public partial class ObejctPickerBeta : ObejctPicker
{
	//unity版本 2017.4.16f1测试
	class SearchBuf
	{
		public string name;
		public int instanceID;
	}

	List<SearchBuf> searchBuf = new List<SearchBuf>(8192);

	public static new ObejctPickerBeta Create(Action<ObejctPicker> callBack = null, ObjectType objectType = ObjectType.None, string[] search_folder = null, InputBuf buf = null)
	{
		return Create<ObejctPickerBeta>(callBack, objectType, search_folder, buf);
	}

	private void SearchAllAssets(string filter)
	{
		var assembly = typeof(EditorWindow).Assembly;
		var searchFilter = assembly.CreateInstance("UnityEditor.SearchFilter");
		ParseSearchString(filter, searchFilter);
		var property = new HierarchyProperty(HierarchyType.Assets);
		SetSearchFilter(property, searchFilter);
		property.Reset();
		searchBuf.Clear();
		while (property.Next(null))
		{
			searchBuf.Add(new SearchBuf { name = property.name, instanceID = property.instanceID });
		}
	}

	private void SearchInFolders(string filter, string[] folder)
	{
		var property = new HierarchyProperty(HierarchyType.Assets);
		var searchFilter = typeof(EditorWindow).Assembly.CreateInstance("UnityEditor.SearchFilter");
		ParseSearchString(filter, searchFilter);
		searchBuf.Clear();
		foreach (string folderPath in folder)
		{
			// Set empty filter to ensure we search all assets to find folder
			SetSearchFilter(property, typeof(EditorWindow).Assembly.CreateInstance("UnityEditor.SearchFilter"));
			int folderInstanceID = GetMainAssetInstanceID(folderPath);
			if (property.Find(folderInstanceID, null))
			{
				// Set filter after we found the folder
				SetSearchFilter(property, searchFilter);
				int folderDepth = property.depth;
				int[] expanded = null; // enter all children of folder
				while (property.NextWithDepthCheck(expanded, folderDepth + 1))
				{
					searchBuf.Add(new SearchBuf { name = property.name, instanceID = property.instanceID });
				}
			}
			else
			{
				L12Debug.EditorLogError("AssetDatabase.FindAssets: Folder not found: '" + folderPath + "'");
			}
		}
	}

	static int GetMainAssetInstanceID(string assetPath)
	{
		var fun = typeof(AssetDatabase).GetMethod("GetMainAssetInstanceID", BindingFlags.NonPublic | BindingFlags.Static);
		return (int)fun.Invoke(null, new object[] { assetPath });
	}

	static void ParseSearchString(string filter, object searchFilter)
	{
		var type = typeof(EditorWindow).Assembly.GetType("UnityEditor.SearchUtility");
		var fun = type.GetMethod("ParseSearchString", BindingFlags.NonPublic | BindingFlags.Static);
		fun.Invoke(null, new object[] { filter, searchFilter });
	}

	static void SetSearchFilter(HierarchyProperty property, object searchFilter)
	{
		var fun = typeof(HierarchyProperty).GetMethod("SetSearchFilter", BindingFlags.NonPublic | BindingFlags.Instance);
		fun.Invoke(property, new object[] { searchFilter });
	}

	protected override void SearchByFilter(string filter, string[] folder)
	{
		try
		{
			if (folder != null)
				SearchInFolders(filter, folder);
			else
				SearchAllAssets(filter);
		}
		catch (Exception e)
		{
			throw new Exception("Unity版本更新可能导致此功能失效,请检查\n" + e.Message);
		}
	}

	protected override string GetAssetNameByIndex(int index)
	{
		return CheckIndexInSearchBuf(index) ? searchBuf[index].name : string.Empty;
	}

	public override T GetSelectedObject<T>()
	{
		return CheckIndexInSearchBuf(select_index) ? EditorUtility.InstanceIDToObject(searchBuf[select_index].instanceID) as T : null;
	}

	protected override int GetSearchAnsLength()
	{
		return searchBuf.Count;
	}

	public override string GetSelectedAssetPath()
	{
		return CheckIndexInSearchBuf(select_index) ? AssetDatabase.GetAssetPath(searchBuf[select_index].instanceID) : null;
	}
}