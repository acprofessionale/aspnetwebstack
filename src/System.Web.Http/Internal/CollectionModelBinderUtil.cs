﻿// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.Web.Http.Metadata;
using System.Web.Http.ModelBinding;
using System.Web.Http.ValueProviders;

namespace System.Web.Http.Internal
{
    internal static class CollectionModelBinderUtil
    {
        internal static void CreateOrReplaceCollection<TElement>(ModelBindingContext bindingContext, IEnumerable<TElement> incomingElements, Func<ICollection<TElement>> creator)
        {
            ICollection<TElement> collection = bindingContext.Model as ICollection<TElement>;
            if (collection == null || collection.IsReadOnly)
            {
                collection = creator();
                bindingContext.Model = collection;
            }

            collection.Clear();
            foreach (TElement element in incomingElements)
            {
                collection.Add(element);
            }
        }

        internal static void CreateOrReplaceDictionary<TKey, TValue>(ModelBindingContext bindingContext, IEnumerable<KeyValuePair<TKey, TValue>> incomingElements, Func<IDictionary<TKey, TValue>> creator)
        {
            IDictionary<TKey, TValue> dictionary = bindingContext.Model as IDictionary<TKey, TValue>;
            if (dictionary == null || dictionary.IsReadOnly)
            {
                dictionary = creator();
                bindingContext.Model = dictionary;
            }

            dictionary.Clear();
            foreach (var element in incomingElements)
            {
                if (element.Key != null)
                {
                    dictionary[element.Key] = element.Value;
                }
            }
        }

        // Instantiate a generic binder. 
        // supportedInterfaceType: type that is updatable by this binder
        // openBinderType: model binder type
        // modelMetadata: metadata for the model to bind
        //
        // example: GetGenericBinder(typeof(IList<>), typeof(ListBinder<>), ...) means that the ListBinder<T>
        // type can update models that implement IList<T>, and if for some reason the existing model instance is not
        // updatable the binder will create a List<T> object and bind to that instead. This method will return a ListBinder<T>
        // or null, depending on whether the type and updatability checks succeed.
        internal static IModelBinder GetGenericBinder(Type supportedInterfaceType, Type openBinderType, Type modelType)
        {
            Contract.Assert(supportedInterfaceType != null);
            Contract.Assert(openBinderType != null);
            Contract.Assert(modelType != null);

            Type[] modelTypeArguments = GetGenericBinderTypeArgs(supportedInterfaceType, modelType);

            if (modelTypeArguments == null)
            {
                return null;
            }

            Type closedBinderType = openBinderType.MakeGenericType(modelTypeArguments);
            var binder = (IModelBinder)Activator.CreateInstance(closedBinderType);
            return binder;
        }

        // Get the generic arguments for the binder, based on the model type. Or null if not compatible.
        internal static Type[] GetGenericBinderTypeArgs(Type supportedInterfaceType, Type modelType)
        {
            if (!modelType.IsGenericType || modelType.IsGenericTypeDefinition)
            {
                // not a closed generic type
                return null;
            }

            Type[] modelTypeArguments = modelType.GetGenericArguments();
            if (modelTypeArguments.Length != supportedInterfaceType.GetGenericArguments().Length)
            {
                // wrong number of generic type arguments
                return null;
            }

            return modelTypeArguments;
        }

        // Check if a binder can handle the model type.
        // This may require per-request information (like model metadata's ReadOnly). This is the per-request counterpart to GetGenericBinderTypeArgs.
        internal static bool IsModelCompatibleWithGenericBinder(Type supportedInterfaceType, Type newInstanceType, Type modelType, ModelMetadata modelMetadata)
        {
            Type[] modelTypeArguments = GetGenericBinderTypeArgs(supportedInterfaceType, modelType);

            if (modelTypeArguments == null)
            {
                return false;
            }

            // Is it possible just to change the reference rather than update the collection in-place?
            if (!modelMetadata.IsReadOnly)            
            {
                Type closedNewInstanceType = newInstanceType.MakeGenericType(modelTypeArguments);
                if (modelMetadata.ModelType.IsAssignableFrom(closedNewInstanceType))
                {
                    return true;
                }
            }

            // At this point, we know we can't change the reference, so we need to verify that
            // the model instance can be updated in-place.
            Type closedSupportedInterfaceType = supportedInterfaceType.MakeGenericType(modelTypeArguments);
            if (!closedSupportedInterfaceType.IsInstanceOfType(modelMetadata.Model))
            {
                return false; // not instance of correct interface
            }

            Type closedCollectionType = TypeHelper.ExtractGenericInterface(closedSupportedInterfaceType, typeof(ICollection<>));
            bool collectionInstanceIsReadOnly = (bool)closedCollectionType.GetProperty("IsReadOnly").GetValue(modelMetadata.Model, null);

            return !collectionInstanceIsReadOnly;            
        }

        [SuppressMessage("Microsoft.Globalization", "CA1304:SpecifyCultureInfo", MessageId = "System.Web.Http.ValueProviders.ValueProviderResult.ConvertTo(System.Type)", Justification = "The ValueProviderResult already has the necessary context to perform a culture-aware conversion.")]
        internal static IEnumerable<string> GetIndexNamesFromValueProviderResult(ValueProviderResult valueProviderResultIndex)
        {
            IEnumerable<string> indexNames = null;
            if (valueProviderResultIndex != null)
            {
                string[] indexes = (string[])valueProviderResultIndex.ConvertTo(typeof(string[]));
                if (indexes != null && indexes.Length > 0)
                {
                    indexNames = indexes;
                }
            }
            return indexNames;
        }

        internal static IEnumerable<string> GetZeroBasedIndexes()
        {
            int i = 0;
            while (true)
            {
                yield return i.ToString(CultureInfo.InvariantCulture);
                i++;
            }
        }

        // Returns the generic type arguments for the model type if updatable, else null.
        // supportedInterfaceType: open type (like IList<>) of supported interface, must implement ICollection<>
        // newInstanceType: open type (like List<>) of object that will be created, must implement supportedInterfaceType
        internal static Type[] GetTypeArgumentsForUpdatableGenericCollection(Type supportedInterfaceType, Type newInstanceType, ModelMetadata modelMetadata)
        {
            // Check that we can extract proper type arguments from the model.
            if (!modelMetadata.ModelType.IsGenericType || modelMetadata.ModelType.IsGenericTypeDefinition)
            {
                // not a closed generic type
                return null;
            }

            Type[] modelTypeArguments = modelMetadata.ModelType.GetGenericArguments();
            if (modelTypeArguments.Length != supportedInterfaceType.GetGenericArguments().Length)
            {
                // wrong number of generic type arguments
                return null;
            }

            // Is it possible just to change the reference rather than update the collection in-place?
            if (!modelMetadata.IsReadOnly)
            {
                Type closedNewInstanceType = newInstanceType.MakeGenericType(modelTypeArguments);
                if (modelMetadata.ModelType.IsAssignableFrom(closedNewInstanceType))
                {
                    return modelTypeArguments;
                }
            }

            // At this point, we know we can't change the reference, so we need to verify that
            // the model instance can be updated in-place.
            Type closedSupportedInterfaceType = supportedInterfaceType.MakeGenericType(modelTypeArguments);
            if (!closedSupportedInterfaceType.IsInstanceOfType(modelMetadata.Model))
            {
                return null; // not instance of correct interface
            }

            Type closedCollectionType = TypeHelper.ExtractGenericInterface(closedSupportedInterfaceType, typeof(ICollection<>));
            bool collectionInstanceIsReadOnly = (bool)closedCollectionType.GetProperty("IsReadOnly").GetValue(modelMetadata.Model, null);
            if (collectionInstanceIsReadOnly)
            {
                return null;
            }
            else
            {
                return modelTypeArguments;
            }
        }
    }
}
