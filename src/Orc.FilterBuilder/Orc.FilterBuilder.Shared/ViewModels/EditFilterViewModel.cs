﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="EditFilterViewModel.cs" company="WildGums">
//   Copyright (c) 2008 - 2016 WildGums. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------


namespace Orc.FilterBuilder.ViewModels
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Catel;
    using Catel.Collections;
    using Catel.Data;
    using Catel.IoC;
    using Catel.MVVM;
    using Catel.Runtime.Serialization.Xml;
    using Catel.Services;
    using Catel.Threading;
    using Models;
    using Services;

    public class EditFilterViewModel : ViewModelBase
    {
        #region Fields
        private readonly FilterScheme _originalFilterScheme;
        private readonly IReflectionService _reflectionService;
        private readonly IXmlSerializer _xmlSerializer;
        private readonly IMessageService _messageService;
        private readonly IServiceLocator _serviceLocator;
        private readonly ILanguageService _languageService;

        private bool _isFilterDirty;
        #endregion

        #region Constructors
        public EditFilterViewModel(FilterSchemeEditInfo filterSchemeEditInfo, IXmlSerializer xmlSerializer,
            IMessageService messageService, IServiceLocator serviceLocator, ILanguageService languageService)
        {
            Argument.IsNotNull(() => filterSchemeEditInfo);
            Argument.IsNotNull(() => xmlSerializer);
            Argument.IsNotNull(() => messageService);
            Argument.IsNotNull(() => serviceLocator);
            Argument.IsNotNull(() => languageService);

            _xmlSerializer = xmlSerializer;
            _messageService = messageService;
            _serviceLocator = serviceLocator;
            _languageService = languageService;

            PreviewItems = new FastObservableCollection<object>();
            RawCollection = filterSchemeEditInfo.RawCollection;
            EnableAutoCompletion = filterSchemeEditInfo.EnableAutoCompletion;
            AllowLivePreview = filterSchemeEditInfo.AllowLivePreview;
            EnableLivePreview = filterSchemeEditInfo.AllowLivePreview;

            var filterScheme = filterSchemeEditInfo.FilterScheme;
            _originalFilterScheme = filterScheme;

            _reflectionService = _serviceLocator.ResolveType<IReflectionService>(filterScheme.Scope);

            DeferValidationUntilFirstSaveCall = true;

            FilterScheme = new FilterScheme
            {
                Scope = _originalFilterScheme.Scope
            };

            AddGroupCommand = new Command<ConditionGroup>(OnAddGroup);
            AddExpressionCommand = new Command<ConditionGroup>(OnAddExpression);
            DeleteConditionItem = new Command<ConditionTreeItem>(OnDeleteCondition, OnDeleteConditionCanExecute);
        }
        #endregion

        #region Properties
        public override string Title
        {
            get { return "Filter scheme"; }
        }

        public string FilterSchemeTitle { get; set; }
        public FilterScheme FilterScheme { get; private set; }
        public bool EnableAutoCompletion { get; private set; }
        public bool AllowLivePreview { get; private set; }
        public bool EnableLivePreview { get; set; }

        public IEnumerable RawCollection { get; private set; }
        public FastObservableCollection<object> PreviewItems { get; private set; }

        public List<IPropertyMetadata> InstanceProperties { get; private set; }

        public Command<ConditionGroup> AddGroupCommand { get; private set; }
        public Command<ConditionGroup> AddExpressionCommand { get; private set; }
        public Command<ConditionTreeItem> DeleteConditionItem { get; private set; }
        #endregion

        #region Methods
        protected override async Task InitializeAsync()
        {
            await base.InitializeAsync();

            InstanceProperties = (await _reflectionService.GetInstancePropertiesAsync(_originalFilterScheme.TargetType)).Properties;

            using (var memoryStream = new MemoryStream())
            {
                _xmlSerializer.Serialize(_originalFilterScheme, memoryStream);
                memoryStream.Position = 0L;
                _xmlSerializer.Deserialize(FilterScheme, memoryStream);
            }

            FilterScheme.EnsureIntegrity(_reflectionService);
            FilterScheme.Scope = _originalFilterScheme.Scope;
            FilterSchemeTitle = FilterScheme.Title;

            RaisePropertyChanged(() => FilterScheme);

            UpdatePreviewItems();

            FilterScheme.Updated += OnFilterSchemeUpdated;
        }

        protected override async Task CloseAsync()
        {
            FilterScheme.Updated -= OnFilterSchemeUpdated;

            await base.CloseAsync();
        }

        private void OnFilterSchemeUpdated(object sender, EventArgs e)
        {
            _isFilterDirty = true;

            UpdatePreviewItems();
        }

        protected override void ValidateFields(List<IFieldValidationResult> validationResults)
        {
            if (string.IsNullOrEmpty(FilterSchemeTitle))
            {
                validationResults.Add(FieldValidationResult.CreateError("FilterSchemeTitle", _languageService.GetString("FilterBuilder_FieldIsRequired")));
            }

            base.ValidateFields(validationResults);
        }

        protected override async Task<bool> CancelAsync()
        {
            if (_isFilterDirty)
            {
                if (await _messageService.ShowAsync(_languageService.GetString("FilterBuilder_DiscardChanges"),
                    _languageService.GetString("FilterBuilder_AreYouSure"), MessageButton.YesNo) == MessageResult.No)
                {
                    return false;
                }
            }

            return await base.CancelAsync();
        }

        protected override Task<bool> SaveAsync()
        {
            FilterScheme.Title = FilterSchemeTitle;
            _originalFilterScheme.Update(FilterScheme);

            return TaskHelper<bool>.FromResult(true);
        }

        private bool OnDeleteConditionCanExecute(ConditionTreeItem item)
        {
            if (item == null)
            {
                return false;
            }

            if (!item.IsRoot())
            {
                return true;
            }

            if (FilterScheme.ConditionItems.Count > 1)
            {
                return true;
            }

            return false;
        }

        private void OnDeleteCondition(ConditionTreeItem item)
        {
            if (item.Parent == null)
            {
                FilterScheme.ConditionItems.Remove(item);

                foreach (var conditionItem in FilterScheme.ConditionItems)
                {
                    conditionItem.Items.Remove(item);
                }
            }
            else
            {
                item.Parent.Items.Remove(item);
            }

            _isFilterDirty = true;

            UpdatePreviewItems();
        }

        private void OnAddExpression(ConditionGroup group)
        {
            var propertyExpression = new PropertyExpression();
            propertyExpression.Property = InstanceProperties.FirstOrDefault();
            group.Items.Add(propertyExpression);
            propertyExpression.Parent = group;
        }

        private void OnAddGroup(ConditionGroup group)
        {
            var newGroup = new ConditionGroup();
            group.Items.Add(newGroup);
            newGroup.Parent = group;
        }

        private void OnEnableLivePreviewChanged()
        {
            UpdatePreviewItems();
        }

        private void UpdatePreviewItems()
        {
            if (FilterScheme == null || RawCollection == null)
            {
                return;
            }

            if (!AllowLivePreview)
            {
                return;
            }

            if (!EnableLivePreview)
            {
                PreviewItems.Clear();
                return;
            }

            FilterScheme.Apply(RawCollection, PreviewItems);
        }
        #endregion
    }
}