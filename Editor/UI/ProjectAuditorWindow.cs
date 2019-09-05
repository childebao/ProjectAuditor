﻿using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace Unity.ProjectAuditor.Editor
{
    class ProjectAuditorWindow : EditorWindow
    {       
        private ProjectAuditor m_ProjectAuditor;
        private ProjectReport m_ProjectReport;
        private IssueTable m_IssueTable;

        private bool m_EnableCPU = true;
        private bool m_EnableGPU = true;
        private bool m_EnableMemory = true;
        private bool m_EnableBuildSize = true;
        private bool m_EnableLoadTimes = true;
        private bool m_EnablePackages = false;
//        private bool m_EnableResolvedItems = false;

        private IssueCategory m_ActiveMode = IssueCategory.ApiCalls;
      
        string[] ReportModeStrings = {
            "API Calls",
            "Project Settings"
        };

        enum Area{
            CPU,
            GPU,
            Memory,
            BuildSize,
            LoadTimes
        }
        
        string[] AreaEnumStrings = {
            "CPU",
            "GPU",
            "Memory",
            "Build Size",
            "Load Times"
        };


        internal static class Styles
        {
            public static readonly GUIStyle Toolbar = "Toolbar";
            public static readonly GUIContent WindowTitle = new GUIContent("Project Auditor");
            public static readonly GUIContent AnalyzeButton = new GUIContent("Analyze", "Analyze Project and list all issues found.");
            public static readonly GUIContent ReloadButton = new GUIContent("Reload DB", "Reload Issue Definition files.");
            public static readonly GUIContent SerializeButton = new GUIContent("Serialize", "Serialize project report to file.");

            public static readonly GUIContent ResolvedHeader = new GUIContent("Resolved?", "Issues that have already been looked at");
            public static readonly GUIContent AreaHeader = new GUIContent("Area", "The area the issue might have an impact on");
            public static readonly GUIContent DescriptionHeader = new GUIContent("Description", "Issue description");
            public static readonly GUIContent LocationHeader = new GUIContent("Location", "Path to the script file");

            public static readonly string HelpText =
@"Project Auditor is an experimental static analysis tool for Unity Projects.
This tool will analyze scripts and project settings of any Unity project
and report a list a possible problems that might affect performance, memory and other areas.

To Analyze the project:
* Click on Analyze.

Once the project is analyzed, the tool displays list of issues.
At the moment there are two types of issues: API calls or Project Settings. The tool allows the user to switch between the two.
In addition, it is possible to filter issues by area (CPU/Memory/etc...).

To generate a report, click on the Serialize button.
To reload the issue database definition, click on Reload DB.";
        }

        private void OnEnable()
        {
            m_ProjectAuditor = new ProjectAuditor();
        }

        private void OnGUI()
        {
            DrawToolbar();

            if (m_IssueTable != null)
            {
                var activeMode = m_ActiveMode;
                m_ActiveMode = (IssueCategory)GUILayout.Toolbar((int)m_ActiveMode, ReportModeStrings);

                if (activeMode != m_ActiveMode)
                {
                    // the user switched view
                    RefreshDisplay();
                }                    
                
                Rect r = EditorGUILayout.GetControlRect(GUILayout.ExpandHeight(true));
                m_IssueTable.OnGUI(r);

                DrawDetails();
            }
        }

        bool ShouldDisplay(ProjectIssue issue)
        {
            string category = issue.category;

            string url = issue.url;
            if (!m_EnablePackages && category.Equals(Editor.IssueCategory.ApiCalls.ToString()) &&
                (url.Contains("Library/PackageCache/") || url.Contains("Resources/PackageManager/BuiltInPackages/")))
                return false;

// temporarily disabled Resolve Items button since there might be issues that have just been checked but are still shown in the list
//            if (!m_EnableResolvedItems && issue.resolved == true)
//                return false;

            string area = issue.def.area;
            if (m_EnableCPU && area.Contains(AreaEnumStrings[(int)Area.CPU]))
                return true;
            if (m_EnableGPU && area.Contains(AreaEnumStrings[(int)Area.GPU]))
                return true;
            if (m_EnableMemory && area.Contains(AreaEnumStrings[(int)Area.Memory]))
                return true;
            if (m_EnableBuildSize && area.Contains(AreaEnumStrings[(int)Area.BuildSize]))
                return true;
            if (m_EnableLoadTimes && area.Contains(AreaEnumStrings[(int)Area.LoadTimes]))
                return true;

            return false;
        }

        private void Analyze()
        {
            m_ProjectReport = m_ProjectAuditor.Audit();
            RefreshDisplay();
        }

        private void RefreshDisplay()
        {
            if (m_ProjectReport == null)
                return;

            MultiColumnHeaderState.Column[] columns = new MultiColumnHeaderState.Column[]
            {
                new MultiColumnHeaderState.Column
                {
                    headerContent = Styles.ResolvedHeader,
                    width = 80,
                    minWidth = 80,
                    autoResize = true
                },
                new MultiColumnHeaderState.Column
                {
                    headerContent = Styles.AreaHeader,
                    width = 100,
                    minWidth = 100,
                    autoResize = true
                },
                new MultiColumnHeaderState.Column
                {
                    headerContent = Styles.DescriptionHeader,
                    width = 300,
                    minWidth = 100,
                    autoResize = true
                },
                              
            };

            var columnsList = new List<MultiColumnHeaderState.Column>(columns);
            
            if (m_ActiveMode == IssueCategory.ApiCalls)
                columnsList.Add(new MultiColumnHeaderState.Column
                {
                    headerContent = Styles.LocationHeader,
                    width = 900,
                    minWidth = 400,
                    autoResize = true
                } );

            var issues = m_ProjectReport.GetIssues(m_ActiveMode);
            
            var filteredList = issues.Where(x => ShouldDisplay(x));

            m_IssueTable = new IssueTable(new TreeViewState(),
                new MultiColumnHeader(new MultiColumnHeaderState(columnsList.ToArray())), filteredList.ToArray());
        }

        private void Reload()
        {
            m_ProjectAuditor.LoadDatabase();
            m_IssueTable = null;
        }

        private void Serialize()
        {
            if (m_ProjectReport != null)
                m_ProjectReport.WriteToFile();
        }

        private void DrawDetails()
        {            
            if (m_IssueTable.HasSelection())
            {               
                var issues = m_ProjectReport.GetIssues(m_ActiveMode);
                
                EditorStyles.textField.wordWrap = true;

                //            var index = m_IssueTable.GetSelection()[0];
                //            var issue = m_ProjectAuditor.m_ProjectIssues[index];

                var displayIndex = m_IssueTable.GetSelection()[0];
                int listIndex = 0;
                int i = 0;

                for (; i < issues.Count; ++i)
                {
                    if (ShouldDisplay(issues[i]))
                    {
                        if (listIndex == displayIndex)
                        {
                            break;
                        }
                        ++listIndex;
                    }
                }

                var issue = issues[i];

                // TODO: use an Issue interface, to define how to display different categories
                string text = string.Empty;

                text = "Issue: " + issue.def.problem;
                EditorGUILayout.TextArea(text, GUILayout.Height(100)/*, GUILayout.ExpandHeight(true)*/ );

                text = "Recommendation: " + issue.def.solution;
                EditorGUILayout.TextArea(text, GUILayout.Height(100)/*, GUILayout.ExpandHeight(true)*/);
                EditorStyles.textField.wordWrap = false;
            }
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(Styles.Toolbar);

            if (GUILayout.Button(Styles.AnalyzeButton, GUILayout.ExpandWidth(true), GUILayout.Width(80)))
                Analyze();
            if (GUILayout.Button(Styles.ReloadButton, GUILayout.ExpandWidth(true), GUILayout.Width(80)))
                Reload();

            if (m_ProjectReport == null)
            {
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginVertical(GUI.skin.box);
         
                GUIStyle helpStyle = new GUIStyle(EditorStyles.textField);
                helpStyle.wordWrap = true;

                EditorGUILayout.LabelField(Styles.HelpText, helpStyle);
                EditorGUILayout.EndHorizontal();                
            }
            else
            {
                if (GUILayout.Button(Styles.SerializeButton, GUILayout.ExpandWidth(true), GUILayout.Width(80)))
                    Serialize();

                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal(Styles.Toolbar);
                GUILayout.Label("Filter By:", GUILayout.ExpandWidth(true), GUILayout.Width(80));

                EditorGUI.BeginChangeCheck();

                m_EnableMemory = EditorGUILayout.ToggleLeft(AreaEnumStrings[(int)Area.Memory], m_EnableMemory, GUILayout.Width(100));
                m_EnableCPU = EditorGUILayout.ToggleLeft(AreaEnumStrings[(int)Area.CPU], m_EnableCPU, GUILayout.Width(100));
                m_EnableGPU = EditorGUILayout.ToggleLeft(AreaEnumStrings[(int)Area.GPU], m_EnableGPU, GUILayout.Width(100));
                m_EnableBuildSize = EditorGUILayout.ToggleLeft(AreaEnumStrings[(int)Area.BuildSize], m_EnableBuildSize, GUILayout.Width(100));
                m_EnableLoadTimes = EditorGUILayout.ToggleLeft(AreaEnumStrings[(int)Area.LoadTimes], m_EnableLoadTimes, GUILayout.Width(100));
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal(Styles.Toolbar);
                GUILayout.Label("", GUILayout.ExpandWidth(true), GUILayout.Width(80));
                m_EnablePackages = EditorGUILayout.ToggleLeft("Packages", m_EnablePackages, GUILayout.Width(100));
    //            m_EnableResolvedItems = EditorGUILayout.ToggleLeft("Resolved Items", m_EnableResolvedItems, GUILayout.Width(100));
                EditorGUILayout.EndHorizontal();

                if (EditorGUI.EndChangeCheck())
                {
                    RefreshDisplay();
                }
            }
        }

        
        
#if UNITY_2018_1_OR_NEWER
        [MenuItem("Window/Analysis/Project Auditor")]
#else
        [MenuItem("Window/Project Auditor")]
#endif
        public static ProjectAuditorWindow ShowWindow()
        {
            var wnd = GetWindow(typeof(ProjectAuditorWindow)) as ProjectAuditorWindow;
            if (wnd != null)
            {
                wnd.titleContent = Styles.WindowTitle;
            }
            return wnd;
        }
    }
}