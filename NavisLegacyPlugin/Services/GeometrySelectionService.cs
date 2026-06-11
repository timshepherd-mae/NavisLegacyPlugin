using System;
using Autodesk.Navisworks.Api;

public static class GeometrySelectionService
{
	public static event Action SelectionChanged;

	private static ModelItemCollection _selectionA;
	public static ModelItemCollection SelectionA
	{
		get => _selectionA;
		set
		{
			_selectionA = value;
			System.Diagnostics.Debug.WriteLine($"SET A: {value?.Count}");
			SelectionChanged?.Invoke();
		}
	}

	private static ModelItemCollection _selectionB;
	public static ModelItemCollection SelectionB
	{
		get => _selectionB;
		set
		{
			_selectionB = value;
			System.Diagnostics.Debug.WriteLine($"SET B: {value?.Count}");
			SelectionChanged?.Invoke();
		}
	}
}