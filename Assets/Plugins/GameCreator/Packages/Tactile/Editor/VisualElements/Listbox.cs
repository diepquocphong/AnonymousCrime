using System;
using System.Collections.Generic;

using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

using GameCreator.Runtime.Common;
using GameCreator.Editor.Common;

namespace Niam.Editor.Tactile
{
    internal class Listbox : ListView
    {
        private const string USS_PATH = EditorPaths.PACKAGES + "Tactile/Editor/Stylesheets/listbox";
        public const string DATA_KEY = "{0}.Array.data[{1}]";

        public static readonly string ClassHeader = "listbox-header";

        public static readonly string ClassItem = "listbox-item";
        public static readonly string NameItemIndexer = "listbox-item--";
        public static readonly string ClassItemNonFirst = ClassItem + "--non-first";
        public static readonly string ClassItemHead = ClassItem + "__head";
        public static readonly string ClassItemHeadButton = ClassItemHead + "-button";
        public static readonly string ClassItemHeadReorder = ClassItemHead + "-reorder";
        public static readonly string ClassItemHeadDelete = ClassItemHead + "-delete";
        public static readonly string ClassItemHeadDuplicate = ClassItemHead + "-duplicate";
        public static readonly string ClassItemHeadToggle = ClassItemHead + "-toggle";
        public static readonly string ClassItemHeadToggleNonReorder = ClassItemHeadToggle + "--non-reorder";
        public static readonly string ClassItemBody = ClassItem + "__body";

        public static readonly string ClassFooter = "listbox-footer";
        public static readonly string ClassFooterDrop = ClassFooter + "__drop";
        public static readonly string ClassFooterAdd = ClassFooter + "__add";
        public static readonly string ClassFooterBtn = ClassFooter + "__btn";
        public static readonly string ClassFooterBtnLast = ClassFooterBtn + "--last";

        public static readonly string NameItemHeadImage = "listbox-item-head-image";

        // MEMBERS: -------------------------------------------------------------------------------

        private SerializedProperty m_PropertyList;
        private SerializedObject m_SerializedObject;

        protected static readonly IIcon IconDrop = new IconSquareOutline(ColorTheme.Type.TextLight);
        protected static readonly IIcon IconReorder = new IconDrag(ColorTheme.Type.TextLight);
        protected static readonly IIcon IconDuplicate = new IconDuplicate(ColorTheme.Type.TextNormal);
        protected static readonly IIcon IconDelete = new IconMinus(ColorTheme.Type.TextNormal);
        protected static readonly IIcon IconDeleteAll = new IconTrashOutline(ColorTheme.Type.TextNormal);
        protected static readonly IIcon IconCollapse = new IconCollapse(ColorTheme.Type.TextNormal);
        protected static readonly IIcon IconExpand = new IconExpand(ColorTheme.Type.TextNormal);

        protected virtual  IIcon IconItem => new IconSquareSolid(ColorTheme.Type.TextNormal);
        protected virtual IIcon IconFooter => new IconPlus(ColorTheme.Type.TextLight);

        protected virtual string FooterText => "Add Element...";

        protected virtual bool ShowReorder => true;
        protected virtual bool ShowDragDrop => false;
        protected virtual bool ShowDuplicate => true;
        protected virtual bool ShowAddFooter => true;
        protected virtual bool AllowUndoRedo => true;
        protected virtual bool ShowFoldToggle => true;
        protected virtual bool ShowDeleteAll => true;

        protected VisualElement m_Header;
        protected VisualElement m_Footer;

        private Button m_BtnCollapseExpand;
        private Image m_ImageCollapseExpand;
        private List<bool> m_ItemFoldStates = new List<bool>();

        // PROPERTIES: ----------------------------------------------------------------------------

        public SerializedProperty PropertyList => this.m_PropertyList;
        public SerializedObject SerializedObject => this.m_SerializedObject;

        // EVENTS: --------------------------------------------------------------------------------
        
        public event Action EventItemFieldChanged;
        public event Action EventItemsLengthChanged;

        // CONSTRUCTOR: ---------------------------------------------------------------------------
        
        public Listbox() : base()
        {
            this.selectionType = SelectionType.None;
            this.virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight;

            this.reorderable = this.ShowReorder;
            this.reorderMode = ListViewReorderMode.Simple;

            this.showBorder = false;
            this.showFoldoutHeader = false;
            this.showAddRemoveFooter = false;
            this.showBoundCollectionSize = false;

            #if UNITY_6000_0_OR_NEWER
            this.canStartDrag += this.CanDragAndDrop;
            #endif
            
            this.m_Header = new VisualElement();
            this.m_Header.AddToClassList(ClassHeader);
            this.hierarchy.Insert(0, this.m_Header);

            if (this.ShowAddFooter)
            {
                this.m_Footer = new VisualElement();
                this.m_Footer.AddToClassList(ClassFooter);
                this.hierarchy.Add(this.m_Footer);

                if (this.ShowDragDrop)
                {
                    var dropArea = new VisualElement();
                    var dropAImage = new Image { image = IconDrop.Texture };
                    dropArea.AddToClassList(ClassFooterDrop);
                    dropArea.Add(dropAImage);
                    this.m_Footer.Add(dropArea);
                }

                var btnAdd = new Button();
                btnAdd.AddToClassList(ClassFooterAdd);
                btnAdd.AddToClassList(ClassFooterBtnLast);
                btnAdd.clicked += this.AddItem;
                this.m_Footer.Add(btnAdd);

                var addImage = new Image { image = IconFooter.Texture };
                btnAdd.Add(addImage);

                var addText = new Label(this.FooterText);
                btnAdd.Add(addText);

                if (this.ShowFoldToggle)
                {
                    this.m_BtnCollapseExpand = new Button();
                    this.m_BtnCollapseExpand.AddToClassList(ClassFooterBtn);
                    this.m_BtnCollapseExpand.clicked += this.ToggleFold;
                    this.m_Footer.Add(this.m_BtnCollapseExpand);

                    this.m_ImageCollapseExpand = new Image();
                    this.m_BtnCollapseExpand.Add(this.m_ImageCollapseExpand);

                    btnAdd.RemoveFromClassList(ClassFooterBtnLast);
                    this.m_BtnCollapseExpand.AddToClassList(ClassFooterBtnLast);
                }

                this.RefreshCollapseExpandIcon();
                
                if (this.ShowDeleteAll)
                {
                    var btnDelete = new Button();
                    btnDelete.AddToClassList(ClassFooterBtn);
                    btnDelete.tooltip = "Delete All";
                    btnDelete.clicked += this.DeleteAllItem;
                    this.m_Footer.Add(btnDelete);

                    var deleteIcon = new Image() { image = IconDeleteAll.Texture};
                    btnDelete.Add(deleteIcon);
                    btnDelete.AddToClassList(ClassFooterBtnLast);
                    btnAdd.RemoveFromClassList(ClassFooterBtnLast);
                    this.m_BtnCollapseExpand.RemoveFromClassList(ClassFooterBtnLast);
                }
            }

            StyleSheet[] sheets = StyleSheetUtils.Load(USS_PATH);
            foreach (StyleSheet styleSheet in sheets) this.styleSheets.Add(styleSheet);
        }

        public Listbox(SerializedProperty propertyList) : this()
        {
            this.BindListbox(propertyList);
        }

        public void BindListbox(SerializedProperty propertyList)
        {
            if (propertyList == null) return;

            this.m_PropertyList = propertyList;
            this.m_SerializedObject = propertyList.serializedObject;

            this.makeItem = this.MakeItem;
            this.bindItem = this.BindItem;
            this.unbindItem = this.UnbindItem;

            this.BindProperty(propertyList);

            this.UnregisterCallback<SerializedPropertyChangeEvent>(this.OnItemPropertyChanged);
            this.RegisterCallback<SerializedPropertyChangeEvent>(this.OnItemPropertyChanged);
            this.InitializeFoldStates();
        }

        public void UnBindListbox()
        {
            this.m_PropertyList = null;
            this.m_SerializedObject = null;
            this.Unbind();

            this.makeItem = null;
            this.bindItem = null;
            this.unbindItem = null;

            this.UnregisterCallback<SerializedPropertyChangeEvent>(this.OnItemPropertyChanged);
            this.m_ItemFoldStates.Clear();
        }

        // CALLBACKS: -----------------------------------------------------------------------------

        private void OnItemPropertyChanged(SerializedPropertyChangeEvent evt)
        {
            this.EventItemFieldChanged?.Invoke();
        }

        #if UNITY_6000_0_OR_NEWER
        private bool CanDragAndDrop(CanStartDragArgs args)
        {
            if (!this.ShowReorder) return false;

            VisualElement container = this.Q("unity-content-container");
            if (container != null)
            {
                Vector2 mousePosition = Event.current.mousePosition;
                foreach (var child in container.Children())
                {
                    var area = child.Q(null, ClassItemHeadReorder);
                    if (area == null) continue;
                    if (!area.worldBound.Contains(mousePosition)) continue;
                    return true;
                }
            }

            return false;
        }
        #endif

        private void OnItemFoldToggle(ChangeEvent<bool> evt)
        {
            if (evt.target is not Foldout foldout) return;
            if (!foldout.ClassListContains("listbox-item")) return;
            if (string.IsNullOrEmpty(foldout.name)) return;

            int index = int.Parse(foldout.name.Replace(NameItemIndexer, ""));
            this.SetItemState(index, evt.newValue);
            this.RefreshCollapseExpandIcon();
            evt.StopPropagation();
        }

        // LISTVIEW METHODS: ----------------------------------------------------------------------

        private VisualElement MakeItem()
        {
            var foldout = new Foldout();
            foldout.SetValueWithoutNotify(false);
            foldout.AddToClassList(ClassItem);
            foldout.RegisterCallback<ChangeEvent<bool>>(this.OnItemFoldToggle);

            var head = new VisualElement();
            head.AddToClassList(ClassItemHead);
            foldout.hierarchy.Insert(0, head);

            if (this.ShowReorder)
            {
                var reorder = new VisualElement();
                reorder.AddToClassList(ClassItemHeadReorder);
                head.Add(reorder);

                var imageReorder = new Image();
                imageReorder.image = IconReorder.Texture;
                imageReorder.pickingMode = PickingMode.Ignore;
                reorder.Add(imageReorder);
            }

            var toggle = foldout.Q<Toggle>(className: Foldout.toggleUssClassName);
            toggle.AddToClassList(ClassItemHeadToggle);
            if (!this.ShowReorder)
            {
                toggle.AddToClassList(ClassItemHeadToggleNonReorder);
            }

            toggle.Clear();
            head.Add(toggle);
            
            var toggleImage = new Image { image = IconItem.Texture };
            toggleImage.name = NameItemHeadImage;
            toggle.Add(toggleImage);

            toggle.Add(this.MakeItemTitle());
            this.MakeItemButtons(head);

            if (this.ShowDuplicate)
            {
                var btnDuplicate = new Button();
                btnDuplicate.AddToClassList(ClassItemHeadButton);
                btnDuplicate.AddToClassList(ClassItemHeadDuplicate);
                btnDuplicate.tooltip = "Duplicate";

                var imageDuplicate = new Image { image = IconDuplicate.Texture };
                btnDuplicate.Add(imageDuplicate);
                head.Add(btnDuplicate);
            }

            var btnDelete = new Button();
            btnDelete.AddToClassList(ClassItemHeadDelete);
            btnDelete.tooltip = "Delete";

            var imageDelete = new Image { image = IconDelete.Texture };
            btnDelete.Add(imageDelete);
            head.Add(btnDelete);

            var bodyContainer = foldout.Q(className: Foldout.contentUssClassName);
            bodyContainer.AddToClassList(ClassItemBody);
            foldout.Add(this.MakeItemContent());            

            return foldout;
        }

        private void BindItem(VisualElement element, int index)
        {
            if (index < 0 || index >= this.m_PropertyList.arraySize) return;

            this.m_SerializedObject.Update();

            if (index != 0)
            {
                element.AddToClassList(ClassItemNonFirst);
                element.Q(null, ClassItemHead).style.height = StyleKeyword.Null;
            }
            else
            {
                element.RemoveFromClassList(ClassItemNonFirst);
                element.Q(null, ClassItemHead).style.height = 21f;
            }

            var property = this.m_PropertyList.GetArrayElementAtIndex(index);
            if (property == null) return;

            if (element is Foldout foldout)
            {
                foldout.name = $"{NameItemIndexer}{index}";
                foldout.SetValueWithoutNotify(this.GetItemState(index, false));
            }

            this.BindTitle(property, element, index);
            this.BindContent(property, element, index);

            if (element.Q(className: ClassItemHeadDuplicate) is Button btnDuplicate)
            {
                btnDuplicate.clickable = null;
                btnDuplicate.clicked += () => this.DuplicateItem(index);
            }

            if (element.Q(className: ClassItemHeadDelete) is Button btnDelete)
            {
                btnDelete.clickable = null;
                btnDelete.clicked += () => this.DeleteItem(index);
            }
        }

        private void UnbindItem(VisualElement element, int index)
        {
            this.UnbindTitle(element, index);
            this.UnbindContent(element, index);
        }

        protected virtual VisualElement MakeItemTitle()
        {
            return new Label 
            { 
                name = "listbox-item-head-title", 
                style = { color = ColorTheme.Get(ColorTheme.Type.TextNormal) }
            };
        }

        protected virtual void MakeItemButtons(VisualElement head)
        { }

        protected virtual VisualElement MakeItemContent()
        {
            return new PropertyField { name = "listbox-item-body-content" };
        }

        protected virtual void BindContent(SerializedProperty property, VisualElement element, int index)
        {
            if (element.Q("listbox-item-body-content") is PropertyField field)
            {
                field.BindProperty(property);
            }
        }

        protected virtual void BindTitle(SerializedProperty property, VisualElement element, int index)
        {
            if (element.Q("listbox-item-head-title") is Label title)
            {
                title.text = $"Element {index}";
            }
        }

        protected virtual void UnbindContent(VisualElement element, int index)
        {
            if (element.Q("listbox-item-body-content") is PropertyField field)
            {
                field.Unbind();
            }
        }

        protected virtual void UnbindTitle(VisualElement element, int index)
        { }

        // PUBLIC METHODS: ------------------------------------------------------------------------

        public void ToggleFold()
        {
            this.CollapseExpandItems(!this.IsAtleastOneIsExpanded());
        }

        public void CollapseExpandItems(bool flag)
        {
            for (int i = 0; i < this.m_PropertyList.arraySize; i++)
                this.SetItemState(i, flag);

            this.RefreshCollapseExpandIcon();
            this.Rebuild();

            // VisualElement container = this.Q("unity-content-container");
            // for (int i = container.childCount - 1; i >= 0; i--)
            // {
            //     if (container[i] is not Foldout foldout) continue;
            //     foldout.SetValueWithoutNotify(flag);
            // }
        }

        public virtual void FillItems(object[] values)
        {
            this.m_SerializedObject.Update();

            this.m_PropertyList.ClearArray();
            for (int i = 0; i < values.Length; ++i)
            {
                this.m_PropertyList.InsertArrayElementAtIndex(i);
                this.m_PropertyList.GetArrayElementAtIndex(i).SetValue(values[i]);
                
                this.SetItemState(i, false);
            }

            this.ApplyModifiedProperties();
            this.RefreshCollapseExpandIcon();
            this.Rebuild();

            this.InvokeItemsLengthChanged();
        }

        public virtual void AddItem()
        {
            this.m_SerializedObject.Update();
            int insertIndex = this.m_PropertyList.arraySize;
            this.m_PropertyList.InsertArrayElementAtIndex(insertIndex);

            this.ApplyModifiedProperties();
            this.SetItemState(insertIndex, true);
            this.RefreshCollapseExpandIcon();
            // this.Rebuild();

            this.InvokeItemsLengthChanged();
        }

        public virtual void InsertItem(int index, object value)
        {
            if (index < 0 || index > this.m_PropertyList.arraySize) return;

            this.m_SerializedObject.Update();
            this.m_PropertyList.InsertArrayElementAtIndex(index);
            this.m_PropertyList.GetArrayElementAtIndex(index).SetValue(value);

            this.ApplyModifiedProperties();
            this.InsertItemState(index, true);
            this.RefreshCollapseExpandIcon();
            // this.Rebuild();

            this.InvokeItemsLengthChanged();
        }

        public virtual void DuplicateItem(int index)
        {
            this.m_SerializedObject.Update();
            if (index < 0) return;

            object source = this.m_PropertyList.GetArrayElementAtIndex(index)
                .GetManagedValue();

            this.m_PropertyList.InsertArrayElementAtIndex(index);
            SerializedProperty newElement = this.m_PropertyList
                .GetArrayElementAtIndex(index + 1);

            CopyPasteUtils.Duplicate(newElement, source);

            this.ApplyModifiedProperties();
            this.InsertItemState(index + 1, true);
            this.RefreshCollapseExpandIcon();
            // this.Rebuild();

            this.InvokeItemsLengthChanged();
        }

        public virtual void DeleteItem(int index)
        {
            this.m_SerializedObject.Update();
            if (this.m_PropertyList.arraySize <= 0) return;

            this.m_PropertyList.DeleteArrayElementAtIndex(index);

            this.ApplyModifiedProperties();
            this.EraseItemState(index);
            this.RefreshCollapseExpandIcon();
            // this.Rebuild();

            this.InvokeItemsLengthChanged();
        }

        public virtual void DeleteAllItem()
        {
            this.m_SerializedObject.Update();
            if (this.m_PropertyList.arraySize <= 0) return;

            this.m_PropertyList.ClearArray();
            
            this.ApplyModifiedProperties();
            this.EraseItemStates();
            this.RefreshCollapseExpandIcon();
            // this.Rebuild();

            this.InvokeItemsLengthChanged();
        }

        // PROTECTED METHODS: ---------------------------------------------------------------------

        protected void RefreshCollapseExpandIcon()
        {
            bool isExpanded = this.IsAtleastOneIsExpanded();

            if (this.m_ImageCollapseExpand != null)
            {
                this.m_ImageCollapseExpand.image = isExpanded 
                    ? IconCollapse.Texture : IconExpand.Texture;
            }

            if (this.m_BtnCollapseExpand != null)
            {
                this.m_BtnCollapseExpand.tooltip = isExpanded 
                    ? "Collapse All" : "Expand All";
            }
        }

        protected bool IsAtleastOneIsExpanded()
        {
            if (this.m_PropertyList != null)
            {
                for (int i = 0; i < this.m_PropertyList.arraySize; i++)
                {
                    if (this.GetItemState(i)) 
                        return true;
                }
            }

            return false;
        }

        protected void ApplyModifiedProperties()
        {
            if (AllowUndoRedo) this.m_SerializedObject.ApplyModifiedProperties();
            else this.m_SerializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        protected void InvokeItemsLengthChanged()
        {
            this.EventItemsLengthChanged?.Invoke();
        }

        // FOLD STATES METHODS: -------------------------------------------------------------------

        protected void InitializeFoldStates()
        {
            this.m_ItemFoldStates.Clear();

            for (int i = 0; i < this.m_PropertyList.arraySize; i++)
            {
                string key = this.GetItemKey(i);
                bool isExpanded = SessionState.GetBool(key, false);

                this.m_ItemFoldStates.Add(isExpanded);
                SessionState.SetBool(key, isExpanded);
            }

            this.RefreshCollapseExpandIcon();
        }

        protected string GetItemKey(int index)
        {
            return string.Format(DATA_KEY, this.m_PropertyList.propertyPath, index);
        }

        protected void SetItemState(int index, bool isExpanded)
        {
            string key = this.GetItemKey(index);

            if (this.m_ItemFoldStates.Count <= index)
                this.m_ItemFoldStates.Add(isExpanded);
            else
                this.m_ItemFoldStates[index] = isExpanded;
            
            SessionState.SetBool(key, isExpanded);
            // Debug.Log($"Set {index} : {isExpanded}");
        }

        protected void InsertItemState(int index, bool isExpanded)
        {
            if (index >= this.m_ItemFoldStates.Count) 
                index = this.m_ItemFoldStates.Count;
            else if (index < 0) 
                index = 0;

            this.m_ItemFoldStates.Insert(index, isExpanded);
            SessionState.SetBool(this.GetItemKey(index), isExpanded);

            for (int i = index + 1; i < this.m_ItemFoldStates.Count; i++)
            {
                SessionState.SetBool(this.GetItemKey(i), this.m_ItemFoldStates[i]);
            }
        }

        protected bool GetItemState(int index, bool defaultState = false)
        {
            if (this.m_ItemFoldStates.Count != this.m_PropertyList.arraySize)
                this.InitializeFoldStates();

            if (index < 0 || index >= this.m_ItemFoldStates.Count) 
                return defaultState;
            
            return this.m_ItemFoldStates[index];
        }

        protected void EraseItemState(int index)
        {
            if (index < 0 || index >= this.m_ItemFoldStates.Count) return;

            this.SetItemState(index, false);
            this.m_ItemFoldStates.RemoveAt(index);
            SessionState.EraseBool(this.GetItemKey(index));
        }

        protected void EraseItemStates()
        {
            for (int i = this.m_ItemFoldStates.Count - 1; i >= 0; i--)
            {
                this.SetItemState(i, false);
                this.m_ItemFoldStates.RemoveAt(i);
                SessionState.EraseBool(this.GetItemKey(i));
            }
        }

    }
}