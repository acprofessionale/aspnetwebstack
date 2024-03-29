﻿// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System.Data.Linq;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Web.Http.Controllers;
using System.Web.Http.ValueProviders;

namespace System.Web.Http.ModelBinding.Binders
{
    // This is a single provider that can work with both byte[] and Binary models.
    public sealed class BinaryDataModelBinderProvider : ModelBinderProvider
    {
        private static readonly ModelBinderProvider[] _providers = new ModelBinderProvider[]
        {
            new SimpleModelBinderProvider(typeof(byte[]), new ByteArrayModelBinder()),
            new SimpleModelBinderProvider(typeof(Binary), new LinqBinaryModelBinder())
        };

        public override IModelBinder GetBinder(HttpConfiguration configuration, Type modelType)
        {
            foreach (ModelBinderProvider provider in _providers)
            {
                IModelBinder binder = provider.GetBinder(configuration, modelType);
                if (binder != null)
                {
                    return binder;
                }
            }
            return null;
        }
                
        private class ByteArrayModelBinder : IModelBinder
        {
            [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "We want to ignore when the data is corrupted")]
            [SuppressMessage("Microsoft.Globalization", "CA1304:SpecifyCultureInfo", MessageId = "System.Web.Http.ValueProviders.ValueProviderResult.ConvertTo(System.Type)", Justification = "The ValueProviderResult already has the necessary context to perform a culture-aware conversion.")]
            public bool BindModel(HttpActionContext actionContext, ModelBindingContext bindingContext)
            {
                ModelBindingHelper.ValidateBindingContext(bindingContext);
                ValueProviderResult valueProviderResult = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);

                // case 1: there was no <input ... /> element containing this data
                if (valueProviderResult == null)
                {
                    return false;
                }

                string base64String = (string)valueProviderResult.ConvertTo(typeof(string));

                // case 2: there was an <input ... /> element but it was left blank
                if (String.IsNullOrEmpty(base64String))
                {
                    return false;
                }

                // Future proofing. If the byte array is actually an instance of System.Data.Linq.Binary
                // then we need to remove these quotes put in place by the ToString() method.
                string realValue = base64String.Replace("\"", String.Empty);
                try
                {
                    bindingContext.Model = ConvertByteArray(Convert.FromBase64String(realValue));
                    return true;
                }
                catch
                {
                    // corrupt data - just ignore
                    return false;
                }
            }

            protected virtual object ConvertByteArray(byte[] originalModel)
            {
                return originalModel;
            }
        }
                
        private class LinqBinaryModelBinder : ByteArrayModelBinder
        {
            protected override object ConvertByteArray(byte[] originalModel)
            {
                return new Binary(originalModel);
            }
        }
    }
}
