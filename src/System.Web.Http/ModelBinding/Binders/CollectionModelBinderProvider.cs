﻿// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Web.Http.Controllers;
using System.Web.Http.Internal;

namespace System.Web.Http.ModelBinding.Binders
{
    public sealed class CollectionModelBinderProvider : ModelBinderProvider
    {
        public override IModelBinder GetBinder(HttpConfiguration configuration, Type modelType)
        {            
            return CollectionModelBinderUtil.GetGenericBinder(typeof(ICollection<>), typeof(CollectionModelBinder<>), modelType); 
        }
    }
}
