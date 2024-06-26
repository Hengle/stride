// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net) and Silicon Studio Corp. (https://www.siliconstudio.co.jp)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Stride.Core.Assets;
using Stride.Core.Annotations;
using Stride.Core.Extensions;
using Stride.Core.Quantum;
using Stride.Assets.Presentation.AssetEditors.AssetCompositeGameEditor.ViewModels;
using Stride.Assets.Presentation.AssetEditors.GameEditor.Services;
using Stride.Assets.Presentation.AssetEditors.GameEditor.ViewModels;
using Stride.Assets.Presentation.AssetEditors.UIEditor.ViewModels;
using Stride.Assets.Presentation.AssetEditors.UILibraryEditor.Services;
using Stride.Assets.Presentation.ViewModel;
using Stride.Assets.UI;
using Stride.Core.Assets.Editor.Annotations;

namespace Stride.Assets.Presentation.AssetEditors.UILibraryEditor.ViewModels
{
    /// <summary>
    /// View model for a <see cref="UILibraryViewModel"/> editor.
    /// </summary>
    [AssetEditorViewModel<UILibraryViewModel>]
    public sealed class UILibraryEditorViewModel : UIEditorBaseViewModel
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="UILibraryEditorViewModel"/> class.
        /// </summary>
        /// <param name="asset">The asset related to this editor.</param>
        public UILibraryEditorViewModel([NotNull] UILibraryViewModel asset)
            : this(asset, x => new UILibraryEditorController(asset, (UILibraryEditorViewModel)x))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="UILibraryEditorViewModel"/> class.
        /// </summary>
        /// <param name="asset">The asset related to this editor.</param>
        /// <param name="controllerFactory">A factory to create the associated <see cref="IEditorGameController"/>.</param>
        private UILibraryEditorViewModel([NotNull] UILibraryViewModel asset, [NotNull] Func<GameEditorViewModel, IEditorGameController> controllerFactory)
            : base(asset, controllerFactory)
        {
        }

        private UILibraryRootViewModel UILibrary => (UILibraryRootViewModel)RootPart;

        /// <inheritdoc/>
        public override void Destroy()
        {
            EnsureNotDestroyed(nameof(UILibraryEditorViewModel));

            UILibrary.Children.CollectionChanged -= RootElementsCollectionChanged;

            base.Destroy();
        }

        /// <inheritdoc/>
        protected override AssetCompositeItemViewModel CreateRootPartViewModel()
        {
            var rootParts = Asset.Asset.Hierarchy.EnumerateRootPartDesigns();
            return new UILibraryRootViewModel(this, (UILibraryViewModel)Asset, rootParts);
        }

        /// <inheritdoc/>
        protected override async Task<bool> InitializeEditor()
        {
            if (!await base.InitializeEditor())
                return false;

            UILibrary.Children.ForEach(r => r.PropertyChanged += RootPropertyChanged);
            UILibrary.Children.CollectionChanged += RootElementsCollectionChanged;

            ActiveRoot = UILibrary.Children.FirstOrDefault();
            return true;
        }

        private void RootElementsCollectionChanged(object sender, [NotNull] NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    foreach (UIElementViewModel root in e.NewItems)
                    {
                        UpdatePublicUIElementsEntry(root.Id.ObjectId, root.Name);
                        root.PropertyChanged += RootPropertyChanged;
                    }
                    break;

                case NotifyCollectionChangedAction.Remove:
                    foreach (UIElementViewModel root in e.OldItems)
                    {
                        root.PropertyChanged -= RootPropertyChanged;
                        UpdatePublicUIElementsEntry(root.Id.ObjectId, null);
                    }
                    break;

                case NotifyCollectionChangedAction.Replace:
                case NotifyCollectionChangedAction.Move:
                case NotifyCollectionChangedAction.Reset:
                    throw new NotSupportedException("Replace, Move and Reset are not supported on this collection.");

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void RootPropertyChanged(object sender, [NotNull] PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(UIElementViewModel.Name))
            {
                var viewModel = (UIElementViewModel)sender;
                UpdatePublicUIElementsEntry(viewModel.Id.ObjectId, viewModel.Name);
            }
        }

        private void UpdatePublicUIElementsEntry(Guid rootId, [CanBeNull] string name)
        {
            var node = NodeContainer.GetNode((UILibraryAsset)Asset.Asset)[nameof(UILibraryAsset.PublicUIElements)].Target;
            var index = new NodeIndex(rootId);

            using (var transaction = UndoRedoService.CreateTransaction())
            {
                // update UILibraryAsset's PublicUIElements collection
                if (string.IsNullOrWhiteSpace(name))
                {
                    // Remove the entry if it exists
                    if (node.Indices.Contains(index))
                    {
                        node.Remove(name, index);
                        UndoRedoService.SetName(transaction, $"Remove '{name}' export from the UILibrary");
                    }
                }
                else
                {
                    if (!node.Indices.Contains(index))
                    {
                        // Note: update would probably work, but we want to remove the item when Undo
                        node.Add(name, index);
                        UndoRedoService.SetName(transaction, $"Add '{name}' export to the UILibrary");
                    }
                    else
                    {
                        node.Update(name, index);
                        UndoRedoService.SetName(transaction, "Update name of export in the UILibrary");
                    }
                }
            }
        }
    }
}
