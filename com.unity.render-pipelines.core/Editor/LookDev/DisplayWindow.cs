using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityEditor.Rendering.LookDev
{
    public interface IViewDisplayer
    {
        Rect GetRect(ViewCompositionIndex index);
        void SetTexture(ViewCompositionIndex index, Texture texture);

        void Repaint();

        event Action<Layout, bool> OnLayoutChanged;

        event Action OnRenderDocAcquisitionTriggered;
        
        event Action<IMouseEvent> OnMouseEventInView;

        event Action<GameObject, ViewCompositionIndex, Vector2> OnChangingObjectInView;
        event Action<Material, ViewCompositionIndex, Vector2> OnChangingMaterialInView;
        event Action<UnityEngine.Object, ViewCompositionIndex, Vector2> OnChangingEnvironmentInView;
        
        event Action OnClosed;
    }
    
    public interface IEnvironmentDisplayer
    {
        void Repaint();

        event Action<UnityEngine.Object> OnAddingEnvironment;
        event Action<int> OnRemovingEnvironment;
        event Action<EnvironmentLibrary> OnChangingEnvironmentLibrary;
    }
    
    /// <summary>
    /// Displayer and User Interaction 
    /// </summary>
    internal class DisplayWindow : EditorWindow, IViewDisplayer, IEnvironmentDisplayer
    {
        static class Style
        {
            internal const string k_IconFolder = @"Packages/com.unity.render-pipelines.core/Editor/LookDev/Icons/";
            internal const string k_uss = @"Packages/com.unity.render-pipelines.core/Editor/LookDev/DisplayWindow.uss";

            public static readonly GUIContent WindowTitleAndIcon = EditorGUIUtility.TrTextContentWithIcon("Look Dev", CoreEditorUtils.LoadIcon(k_IconFolder, "LookDevMainIcon"));
        }
        
        // /!\ WARNING:
        //The following const are used in the uss.
        //If you change them, update the uss file too.
        const string k_MainContainerName = "mainContainer";
        const string k_EnvironmentContainerName = "environmentContainer";
        const string k_ViewContainerName = "viewContainer";
        const string k_FirstViewName = "firstView";
        const string k_SecondViewName = "secondView";
        const string k_ToolbarName = "toolbar";
        const string k_ToolbarRadioName = "toolbarRadio";
        const string k_ToolbarEnvironmentName = "toolbarEnvironment";
        const string k_SharedContainerClass = "container";
        const string k_FirstViewClass = "firstView";
        const string k_SecondViewsClass = "secondView";
        const string k_VerticalViewsClass = "verticalSplit";
        const string k_ShowEnvironmentPanelClass = "showEnvironmentPanel";

        VisualElement m_MainContainer;
        VisualElement m_ViewContainer;
        VisualElement m_EnvironmentContainer;
        ListView m_EnvironmentList;
        EnvironmentElement m_EnvironmentInspector;

        Image[] m_Views = new Image[2];
        

        Layout layout
        {
            get => LookDev.currentContext.layout.viewLayout;
            set
            {
                if (LookDev.currentContext.layout.viewLayout != value)
                {
                    OnLayoutChangedInternal?.Invoke(value, showEnvironmentPanel);
                    ApplyLayout(value);
                }
            }
        }
        
        bool showEnvironmentPanel
        {
            get => LookDev.currentContext.layout.showEnvironmentPanel;
            set
            {
                if (LookDev.currentContext.layout.showEnvironmentPanel != value)
                {
                    OnLayoutChangedInternal?.Invoke(layout, value);
                    ApplyEnvironmentToggling(value);
                }
            }
        }
        
        event Action<Layout, bool> OnLayoutChangedInternal;
        event Action<Layout, bool> IViewDisplayer.OnLayoutChanged
        {
            add => OnLayoutChangedInternal += value;
            remove => OnLayoutChangedInternal -= value;
        }

        event Action OnRenderDocAcquisitionTriggeredInternal;
        event Action IViewDisplayer.OnRenderDocAcquisitionTriggered
        {
            add => OnRenderDocAcquisitionTriggeredInternal += value;
            remove => OnRenderDocAcquisitionTriggeredInternal -= value;
        }

        event Action<IMouseEvent> OnMouseEventInViewPortInternal;
        event Action<IMouseEvent> IViewDisplayer.OnMouseEventInView
        {
            add => OnMouseEventInViewPortInternal += value;
            remove => OnMouseEventInViewPortInternal -= value;
        }

        event Action<GameObject, ViewCompositionIndex, Vector2> OnChangingObjectInViewInternal;
        event Action<GameObject, ViewCompositionIndex, Vector2> IViewDisplayer.OnChangingObjectInView
        {
            add => OnChangingObjectInViewInternal += value;
            remove => OnChangingObjectInViewInternal -= value;
        }

        event Action<Material, ViewCompositionIndex, Vector2> OnChangingMaterialInViewInternal;
        event Action<Material, ViewCompositionIndex, Vector2> IViewDisplayer.OnChangingMaterialInView
        {
            add => OnChangingMaterialInViewInternal += value;
            remove => OnChangingMaterialInViewInternal -= value;
        }

        event Action<UnityEngine.Object, ViewCompositionIndex, Vector2> OnChangingEnvironmentInViewInternal;
        event Action<UnityEngine.Object, ViewCompositionIndex, Vector2> IViewDisplayer.OnChangingEnvironmentInView
        {
            add => OnChangingEnvironmentInViewInternal += value;
            remove => OnChangingEnvironmentInViewInternal -= value;
        }

        event Action OnClosedInternal;
        event Action IViewDisplayer.OnClosed
        {
            add => OnClosedInternal += value;
            remove => OnClosedInternal -= value;
        }

        event Action<UnityEngine.Object> OnAddingEnvironmentInternal;
        event Action<UnityEngine.Object> IEnvironmentDisplayer.OnAddingEnvironment
        {
            add => OnAddingEnvironmentInternal += value;
            remove => OnAddingEnvironmentInternal -= value;
        }

        event Action<int> OnRemovingEnvironmentInternal;
        event Action<int> IEnvironmentDisplayer.OnRemovingEnvironment
        {
            add => OnRemovingEnvironmentInternal += value;
            remove => OnRemovingEnvironmentInternal -= value;
        }

        event Action<EnvironmentLibrary> OnChangingEnvironmentLibraryInternal;
        event Action<EnvironmentLibrary> IEnvironmentDisplayer.OnChangingEnvironmentLibrary
        {
            add => OnChangingEnvironmentLibraryInternal += value;
            remove => OnChangingEnvironmentLibraryInternal -= value;
        }

        void OnEnable()
        {
            //Call the open function to configure LookDev
            // in case the window where open when last editor session finished.
            // (Else it will open at start and has nothing to display).
            if (!LookDev.open)
                LookDev.Open();

            titleContent = Style.WindowTitleAndIcon;

            rootVisualElement.styleSheets.Add(
                AssetDatabase.LoadAssetAtPath<StyleSheet>(Style.k_uss));
            
            CreateToolbar();
            
            m_MainContainer = new VisualElement() { name = k_MainContainerName };
            m_MainContainer.AddToClassList(k_SharedContainerClass);
            rootVisualElement.Add(m_MainContainer);

            CreateViews();
            CreateEnvironment();
            CreateDropAreas();

            ApplyLayout(layout);
            ApplyEnvironmentToggling(showEnvironmentPanel);
        }

        void OnDisable() => OnClosedInternal?.Invoke();

        void CreateToolbar()
        {
            // Layout swapper part
            var toolbarRadio = new ToolbarRadio() { name = k_ToolbarRadioName };
            toolbarRadio.AddRadios(new[] {
                CoreEditorUtils.LoadIcon(Style.k_IconFolder, "LookDevSingle1"),
                CoreEditorUtils.LoadIcon(Style.k_IconFolder, "LookDevSingle2"),
                CoreEditorUtils.LoadIcon(Style.k_IconFolder, "LookDevSideBySideVertical"),
                CoreEditorUtils.LoadIcon(Style.k_IconFolder, "LookDevSideBySideHorizontal"),
                CoreEditorUtils.LoadIcon(Style.k_IconFolder, "LookDevSplit"),
                CoreEditorUtils.LoadIcon(Style.k_IconFolder, "LookDevZone"),
                });
            toolbarRadio.RegisterCallback((ChangeEvent<int> evt)
                => layout = (Layout)evt.newValue);
            toolbarRadio.SetValueWithoutNotify((int)layout);

            // Environment part
            var toolbarEnvironment = new Toolbar() { name = k_ToolbarEnvironmentName };
            var showEnvironmentToggle = new ToolbarToggle() { text = "Show Environment" };
            showEnvironmentToggle.RegisterCallback((ChangeEvent<bool> evt)
                => showEnvironmentPanel = evt.newValue);
            showEnvironmentToggle.SetValueWithoutNotify(showEnvironmentPanel);
            toolbarEnvironment.Add(showEnvironmentToggle);

            //other parts to be completed

            // Aggregate parts
            var toolbar = new Toolbar() { name = k_ToolbarName };
            toolbar.Add(new Label() { text = "Layout:" });
            toolbar.Add(toolbarRadio);
            toolbar.Add(new ToolbarSpacer());
            //to complete


            toolbar.Add(new ToolbarSpacer() { flex = true });

            //TODO: better RenderDoc integration
            toolbar.Add(new ToolbarButton(() => OnRenderDocAcquisitionTriggeredInternal?.Invoke())
            {
                text = "RenderDoc Content"
            });
            
            toolbar.Add(toolbarEnvironment);
            rootVisualElement.Add(toolbar);
        }

        void CreateViews()
        {
            if (m_MainContainer == null || m_MainContainer.Equals(null))
                throw new System.MemberAccessException("m_MainContainer should be assigned prior CreateViews()");

            m_ViewContainer = new VisualElement() { name = k_ViewContainerName };
            m_ViewContainer.AddToClassList(LookDev.currentContext.layout.isMultiView ? k_SecondViewsClass : k_FirstViewClass);
            m_ViewContainer.AddToClassList(k_SharedContainerClass);
            m_MainContainer.Add(m_ViewContainer);
            m_ViewContainer.RegisterCallback<MouseDownEvent>(evt => OnMouseEventInViewPortInternal?.Invoke(evt));
            m_ViewContainer.RegisterCallback<MouseUpEvent>(evt => OnMouseEventInViewPortInternal?.Invoke(evt));
            m_ViewContainer.RegisterCallback<MouseMoveEvent>(evt => OnMouseEventInViewPortInternal?.Invoke(evt));

            m_Views[(int)ViewIndex.First] = new Image() { name = k_FirstViewName, image = Texture2D.blackTexture };
            m_ViewContainer.Add(m_Views[(int)ViewIndex.First]);
            m_Views[(int)ViewIndex.Second] = new Image() { name = k_SecondViewName, image = Texture2D.blackTexture };
            m_ViewContainer.Add(m_Views[(int)ViewIndex.Second]);
        }

        void CreateDropAreas()
        {
            // GameObject or Prefab in view
            new DropArea(new[] { typeof(GameObject) }, m_Views[(int)ViewIndex.First], (obj, localPos) =>
            {
                if (layout == Layout.CustomSplit || layout == Layout.CustomCircular)
                    OnChangingObjectInViewInternal?.Invoke(obj as GameObject, ViewCompositionIndex.Composite, localPos);
                else
                    OnChangingObjectInViewInternal?.Invoke(obj as GameObject, ViewCompositionIndex.First, localPos);
            });
            new DropArea(new[] { typeof(GameObject) }, m_Views[(int)ViewIndex.Second], (obj, localPos)
                => OnChangingObjectInViewInternal?.Invoke(obj as GameObject, ViewCompositionIndex.Second, localPos));

            // Material in view
            new DropArea(new[] { typeof(GameObject) }, m_Views[(int)ViewIndex.First], (obj, localPos) =>
            {
                if (layout == Layout.CustomSplit || layout == Layout.CustomCircular)
                    OnChangingMaterialInViewInternal?.Invoke(obj as Material, ViewCompositionIndex.Composite, localPos);
                else
                    OnChangingMaterialInViewInternal?.Invoke(obj as Material, ViewCompositionIndex.First, localPos);
            });
            new DropArea(new[] { typeof(Material) }, m_Views[(int)ViewIndex.Second], (obj, localPos)
                => OnChangingMaterialInViewInternal?.Invoke(obj as Material, ViewCompositionIndex.Second, localPos));

            // Environment in view
            new DropArea(new[] { typeof(Environment), typeof(Cubemap) }, m_Views[(int)ViewIndex.First], (obj, localPos) =>
            {
                if (layout == Layout.CustomSplit || layout == Layout.CustomCircular)
                    OnChangingEnvironmentInViewInternal?.Invoke(obj, ViewCompositionIndex.Composite, localPos);
                else
                    OnChangingEnvironmentInViewInternal?.Invoke(obj, ViewCompositionIndex.First, localPos);
            });
            new DropArea(new[] { typeof(Environment), typeof(Cubemap) }, m_Views[(int)ViewIndex.Second], (obj, localPos)
                => OnChangingEnvironmentInViewInternal?.Invoke(obj, ViewCompositionIndex.Second, localPos));

            // Environment in library
            new DropArea(new[] { typeof(Environment), typeof(Cubemap) }, m_EnvironmentContainer, (obj, localPos) =>
            {
                //[TODO: check if this come from outside of library]
                OnAddingEnvironmentInternal?.Invoke(obj);
            });
            new DropArea(new[] { typeof(EnvironmentLibrary) }, m_EnvironmentContainer, (obj, localPos) =>
            {
                OnChangingEnvironmentLibraryInternal?.Invoke(obj as EnvironmentLibrary);
                RefreshLibraryDisplay();
            });
        }
        
        void CreateEnvironment()
        {
            if (m_MainContainer == null || m_MainContainer.Equals(null))
                throw new System.MemberAccessException("m_MainContainer should be assigned prior CreateEnvironment()");

            m_EnvironmentContainer = new VisualElement() { name = k_EnvironmentContainerName };
            m_MainContainer.Add(m_EnvironmentContainer);
            if (showEnvironmentPanel)
                m_MainContainer.AddToClassList(k_ShowEnvironmentPanelClass);


            var inspector = new EnvironmentElement(withPreview: false);
            m_EnvironmentList = new ListView();
            m_EnvironmentList.onSelectionChanged += objects =>
            {
                if (objects.Count == 0)
                    inspector.style.visibility = Visibility.Hidden;
                else
                {
                    inspector.style.visibility = Visibility.Visible;
                    inspector.Bind(LookDev.currentContext.environmentLibrary[m_EnvironmentList.selectedIndex], m_EnvironmentList.selectedItem as Image);
                }
            };
            m_EnvironmentContainer.Add(inspector);
            m_EnvironmentContainer.Add(m_EnvironmentList);

            RefreshLibraryDisplay();
        }

        void RefreshLibraryDisplay()
        {
            var library = LookDev.currentContext.environmentLibrary;

            int itemMax = library?.Count ?? 0;
            var items = new List<int>(itemMax);
            for (int i = 0; i < itemMax; i++)
                items.Add(i);

            Func<VisualElement> makeItem = () => new Image();
            Action<VisualElement, int> bindItem = (e, i) =>
            {
                (e as Image).image = EnvironmentElement.GetLatLongThumbnailTexture(library[i], EnvironmentElement.k_SkyThumbnailWidth);
            };

            m_EnvironmentList.itemsSource = items;
            m_EnvironmentList.itemHeight = EnvironmentElement.k_SkyThumbnailHeight + 10;
            m_EnvironmentList.makeItem = makeItem;
            m_EnvironmentList.bindItem = bindItem;
            m_EnvironmentList.selectionType = SelectionType.Single;

            m_EnvironmentList.onItemChosen += obj =>
                EditorGUIUtility.PingObject(library[(int)obj]);
        }

        Rect IViewDisplayer.GetRect(ViewCompositionIndex index)
        {
            switch (index)
            {
                case ViewCompositionIndex.First:
                case ViewCompositionIndex.Composite:    //display composition on first rect
                    return m_Views[(int)ViewIndex.First].contentRect;
                case ViewCompositionIndex.Second:
                    return m_Views[(int)ViewIndex.Second].contentRect;
                default:
                    throw new ArgumentException("Unknown ViewCompositionIndex: " + index);
            }
        }

        void IViewDisplayer.SetTexture(ViewCompositionIndex index, Texture texture)
        {
            switch (index)
            {
                case ViewCompositionIndex.First:
                case ViewCompositionIndex.Composite:    //display composition on first rect
                    if (m_Views[(int)ViewIndex.First].image != texture)
                        m_Views[(int)ViewIndex.First].image = texture;
                    break;
                case ViewCompositionIndex.Second:
                    if (m_Views[(int)ViewIndex.Second].image != texture)
                        m_Views[(int)ViewIndex.Second].image = texture;
                    break;
                default:
                    throw new ArgumentException("Unknown ViewCompositionIndex: " + index);
            }
        }

        void IViewDisplayer.Repaint() => Repaint();

        //[TODO]
        void IEnvironmentDisplayer.Repaint()
        {
            throw new NotImplementedException();
        }

        void ApplyLayout(Layout value)
        {
            switch (value)
            {
                case Layout.HorizontalSplit:
                case Layout.VerticalSplit:
                    if (!m_ViewContainer.ClassListContains(k_FirstViewClass))
                        m_ViewContainer.AddToClassList(k_FirstViewClass);
                    if (!m_ViewContainer.ClassListContains(k_SecondViewsClass))
                        m_ViewContainer.AddToClassList(k_SecondViewsClass);
                    if (value == Layout.VerticalSplit)
                    {
                        m_ViewContainer.AddToClassList(k_VerticalViewsClass);
                        if (!m_ViewContainer.ClassListContains(k_VerticalViewsClass))
                            m_ViewContainer.AddToClassList(k_FirstViewClass);
                    }
                    break;
                case Layout.FullFirstView:
                case Layout.CustomSplit:       //display composition on first rect
                case Layout.CustomCircular:    //display composition on first rect
                    if (!m_ViewContainer.ClassListContains(k_FirstViewClass))
                        m_ViewContainer.AddToClassList(k_FirstViewClass);
                    if (m_ViewContainer.ClassListContains(k_SecondViewsClass))
                        m_ViewContainer.RemoveFromClassList(k_SecondViewsClass);
                    break;
                case Layout.FullSecondView:
                    if (m_ViewContainer.ClassListContains(k_FirstViewClass))
                        m_ViewContainer.RemoveFromClassList(k_FirstViewClass);
                    if (!m_ViewContainer.ClassListContains(k_SecondViewsClass))
                        m_ViewContainer.AddToClassList(k_SecondViewsClass);
                    break;
                default:
                    throw new ArgumentException("Unknown Layout");
            }

            //Add flex direction here
            if (value == Layout.VerticalSplit)
                m_ViewContainer.AddToClassList(k_VerticalViewsClass);
            else if (m_ViewContainer.ClassListContains(k_VerticalViewsClass))
                m_ViewContainer.RemoveFromClassList(k_VerticalViewsClass);
        }

        void ApplyEnvironmentToggling(bool open)
        {
            if (open)
            {
                if (!m_MainContainer.ClassListContains(k_ShowEnvironmentPanelClass))
                    m_MainContainer.AddToClassList(k_ShowEnvironmentPanelClass);
            }
            else
            {
                if (m_MainContainer.ClassListContains(k_ShowEnvironmentPanelClass))
                    m_MainContainer.RemoveFromClassList(k_ShowEnvironmentPanelClass);
            }
        }
    }
}
