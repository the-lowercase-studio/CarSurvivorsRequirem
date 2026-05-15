using Assets.Scripts.Navigation.GridSystem;
using System;
using TMPro;
using UnityEngine;
using NavigationGrid = Assets.Scripts.Navigation.GridSystem.Grid;

namespace Assets.Scripts.Navigation.FlowFieldSystem
{
    [Serializable]
    public class FlowFieldDebugConfiguration
    {
        public NavigationGrid Grid;
        public Transform DebugInfoHolder;
        public float FontSize;
        public float TextYOffset;
        public FlowFieldDebug.DisplayMode DisplayMode;

        public FlowFieldDebugConfiguration(NavigationGrid grid,
                                           Transform debugInfoHolder,
                                           float fontSize = 1f,
                                           float textYOffset = 0,
                                           FlowFieldDebug.DisplayMode displayMode = FlowFieldDebug.DisplayMode.CostField)
        {
            Grid = grid;
            DebugInfoHolder = debugInfoHolder;
            FontSize = fontSize;
            TextYOffset = textYOffset;
            DisplayMode = displayMode;
        }
    }

    public static class FlowFieldDebug
    {
        public enum DisplayMode
        {
            CostField,
            IntegrationField,
            FlowField
        }

        public static void DisplayFlowFieldDebugTextOnGrid(FlowFieldDebugConfiguration configuration)
        {
            bool cellDebugTextWasPreviouslyCreated = configuration.DebugInfoHolder.childCount > 0;
            int currentChildIndex = 0;
            Cell[,] cells = configuration.Grid.Cells;
            for (int i = 0; i < cells.GetLength(0); i++)
            {
                for (int j = 0; j < cells.GetLength(1); j++)
                {
                    Cell cell = cells[i, j];
                    TextMeshPro textComponent;
                    string text = GetCellDebugTextBasedOnMode(cell, configuration.DisplayMode);
                    if (cellDebugTextWasPreviouslyCreated)
                    {
                        textComponent = configuration.DebugInfoHolder.GetChild(currentChildIndex).GetComponent<TextMeshPro>();
                        textComponent.text = text;
                        currentChildIndex++;
                    }
                    else
                    {
                        textComponent = CreateCellDebugText(configuration, cell, text);
                        textComponent.text = text;
                    }
                }
            }
        }

        private static TextMeshPro CreateCellDebugText(FlowFieldDebugConfiguration configuration, Cell cell, string textObjectName)
        {
            GameObject visualizer = new(textObjectName);
            visualizer.transform.parent = configuration.DebugInfoHolder;
            visualizer.transform.SetPositionAndRotation(new Vector3(cell.WorldPos.x, configuration.TextYOffset, cell.WorldPos.z),
                                                        Quaternion.Euler(new Vector3(90, 0, 0)));
            var rectTransform = visualizer.AddComponent<RectTransform>();
            rectTransform.rect.Set(0, 0, configuration.Grid.CellSize, configuration.Grid.CellSize);
            visualizer.AddComponent<MeshRenderer>();
            var textComponent = visualizer.AddComponent<TextMeshPro>();
            textComponent.fontSize = configuration.FontSize;
            textComponent.alignment = TextAlignmentOptions.Center;
            return textComponent;
        }

        private static string GetCellDebugTextBasedOnMode(Cell cell, DisplayMode displayMode) => displayMode switch
        {
            DisplayMode.CostField => cell.Cost.ToString(),
            DisplayMode.IntegrationField => cell.BestCost.ToString(),
            DisplayMode.FlowField => cell.BestDirection.Vector.ToString(),
            _ => ""
        };
    }
}
