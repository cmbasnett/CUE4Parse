﻿using System;
using System.Collections.Generic;
using CUE4Parse.UE4.Objects.UObject;
using Serilog;

namespace CUE4Parse.MappingsProvider
{
    public class Struct
    {
        public readonly TypeMappings Context;
        public string Name;
        public string? SuperType;
        public Lazy<Struct?> Super;
        public Dictionary<int, PropertyInfo> Properties;
        public int PropertyCount;

        public Struct(TypeMappings context, string name, int propertyCount)
        {
            Context = context;
            Name = name;
            PropertyCount = propertyCount;
        }

        public Struct(TypeMappings context, string name, string? superType, Dictionary<int, PropertyInfo> properties, int propertyCount) : this(context, name, propertyCount)
        {
            SuperType = superType;
            Super = new Lazy<Struct?>(() =>
            {
                if (SuperType != null && Context.Types.TryGetValue(SuperType, out var superStruct))
                {
                    return superStruct;
                }

                return null;
            });
            Properties = properties;
        }

        public bool TryGetValue(int i, out PropertyInfo info)
        {
            if (!Properties.TryGetValue(i, out info))
            {
                return i >= PropertyCount && Super.Value != null &&
                       Super.Value.TryGetValue(i - PropertyCount, out info);
            }

            return true;
        }
    }

    public class SerializedStruct : Struct
    {
        public SerializedStruct(TypeMappings context, UStruct struc) : base(context, struc.Name, struc.ChildProperties.Length)
        {
            Super = new Lazy<Struct?>(() =>
            {
                //if (struc.SuperStruct.TryLoad<UStruct>(out var superStruct))
                var superStruct = struc.SuperStruct.Load<UStruct>();
                if (superStruct != null)
                {
                    if (superStruct is UScriptClass)
                    {
                        if (Context.Types.TryGetValue(superStruct.Name, out var scriptStruct))
                        {
                            return scriptStruct;
                        }

                        Log.Warning("Missing prop mappings for type {0}", superStruct.Name);
                        return null;
                    }

                    return new SerializedStruct(Context, superStruct);
                }

                return null;
            });
            Properties = new Dictionary<int, PropertyInfo>();
            for (var i = 0; i < struc.ChildProperties.Length; i++)
            {
                var prop = struc.ChildProperties[i] as FProperty;
                var propInfo = new PropertyInfo(i, prop.Name.Text, new PropertyType(prop), prop.ArrayDim);
                for (int j = 0; j < prop.ArrayDim; j++)
                {
                    Properties[i + j] = propInfo;
                }
            }
        }
    }

    public class PropertyInfo
    {
        public int Index;
        public string Name;
        public int? ArraySize;
        public PropertyType MappingType;

        public PropertyInfo(int index, string name, PropertyType mappingType, int? arraySize = null)
        {
            Index = index;
            Name = name;
            ArraySize = arraySize;
            MappingType = mappingType;
        }
    }

    public class PropertyType
    {
        public string Type;
        public string? StructType;
        public PropertyType? InnerType;
        public PropertyType? ValueType;
        public string? EnumName;
        public bool? IsEnumAsByte;
        public bool? Bool;

        public PropertyType(string type, string? structType = null, PropertyType? innerType = null, PropertyType? valueType = null, string? enumName = null, bool? isEnumAsByte = null, bool? b = null)
        {
            Type = type;
            StructType = structType;
            InnerType = innerType;
            ValueType = valueType;
            EnumName = enumName;
            IsEnumAsByte = isEnumAsByte;
            Bool = b;
        }

        public PropertyType(FProperty prop)
        {
            Type = prop.GetType().Name.Substring(1);
            if (prop is FArrayProperty array)
            {
                InnerType = new PropertyType(array.Inner);
            }
            else if (prop is FByteProperty b)
            {
                EnumName = b.Enum.Load().Name; // TODO if enum is UserDefinedEnum it will fail
            }
            //is FEnumProperty => {
            //enumName = prop.enum
            //}
            else if (prop is FMapProperty map)
            {
                InnerType = new PropertyType(map.KeyProp);
                ValueType = new PropertyType(map.ValueProp);
            }
            else if (prop is FSetProperty set)
            {
                InnerType = new PropertyType(set.ElementProp);
            }
            else if (prop is FStructProperty struc)
            {
                var structClass = struc.Struct.Load<UStruct>();
                StructType = structClass.Name; // TODO load the mappings for that struct too, currently it will fail if it's a serialized struct
            }
        }
    }
}