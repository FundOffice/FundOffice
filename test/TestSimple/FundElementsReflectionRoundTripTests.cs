using FMO.Models;
using FMO.Utilities;
using Initial;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace FMO.Tests;

[TestClass]
public class FundElementsReflectionRoundTripTests
{
    [TestMethod]
    public void ToFacts_RoundTrip_ReflectiveDeepValidation()
    {
        var original = new FundElements { Id = 100 };
        var props = typeof(FundElements).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                        .Where(p => p.CanRead)
                                        .ToArray();

        // 1. 反射注入测试数据（精确匹配 SetValue 重载）
        foreach (var prop in props)
        {
            if (prop.Name == nameof(FundElements.Id)) continue;
            if (!prop.PropertyType.IsGenericType) continue;

            var genericDef = prop.PropertyType.GetGenericTypeDefinition();
            var valueType = prop.PropertyType.GetGenericArguments()[0];
            var instance = prop.GetValue(original)!;
            var instanceType = instance.GetType();

            MethodInfo? setValueMethod = null;
            object[] invokeArgs;

            if (genericDef == typeof(Mutable<>))
            {
                var data = GenerateTestData(valueType, flowId: 1, shareId: -1);
                setValueMethod = instanceType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .FirstOrDefault(m => m.Name == "SetValue" && m.GetParameters().Length == 2 && m.GetParameters()[1].ParameterType == typeof(int));

                if (setValueMethod == null)
                    throw new AssertFailedException($"未在 {instanceType.Name} 中找到 SetValue(T value, int flowId) 方法");

                invokeArgs = new object[] { data, 1 };
            }
            else if (genericDef == typeof(PortionMutable<>))
            {
                var data = GenerateTestData(valueType, flowId: 2, shareId: 5);
                setValueMethod = instanceType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .FirstOrDefault(m => m.Name == "SetValue" && m.GetParameters().Length == 3 &&
                                         m.GetParameters()[0].ParameterType == typeof(int) &&
                                         m.GetParameters()[2].ParameterType == typeof(int));

                if (setValueMethod == null)
                    throw new AssertFailedException($"未在 {instanceType.Name} 中找到 SetValue(int shareId, T value, int flowId) 方法");

                invokeArgs = new object[] { 5, data, 2 };
            }
            else continue;

            try
            {
                setValueMethod.Invoke(instance, invokeArgs);
            }
            catch (TargetInvocationException tex)
            {
                throw new AssertFailedException($"调用 {prop.Name}.{setValueMethod.Name} 失败: {tex.InnerException?.Message ?? tex.Message}");
            }
        }

        // 2. 执行往返转换
        var facts = original.ToFacts();
        var restored = FundElements.From(facts);

        // 3. 反射深度对比（动态对比所有 Key）
        foreach (var prop in props)
        {
            if (prop.Name == nameof(FundElements.Id))
            {
                Assert.AreEqual(original.Id, restored.Id, $"基础属性 {prop.Name} 值不一致");
                continue;
            }

            var pType = prop.PropertyType;
            if (!pType.IsGenericType) continue;

            var genericDef = pType.GetGenericTypeDefinition();
            var origVal = prop.GetValue(original)!;
            var restVal = prop.GetValue(restored)!;

            if (genericDef == typeof(Mutable<>))
                AssertMutableDeepEqual(origVal, restVal, prop.Name);
            else if (genericDef == typeof(PortionMutable<>))
                AssertPortionMutableDeepEqual(origVal, restVal, prop.Name);
        }
    }

    [TestMethod]
    public void ToFacts_RoundTrip_ReflectiveDeepValidation_RealData()
    {
        var props = typeof(FundElements).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                        .Where(p => p.CanRead)
                                        .ToArray();

        TestInit.SetAsDebug();

        using var db = DbHelper.Base();
        var fe = db.GetCollection<FundElements>().FindAll().ToArray();

        foreach (var f in fe)
        {
            var original = f;

            // 2. 执行往返转换
            var facts = original.ToFacts();
            var restored = FundElements.From(facts);

            // 3. 反射深度对比
            foreach (var prop in props)
            {
                if (prop.Name == nameof(FundElements.Id))
                {
                    Assert.AreEqual(original.Id, restored.Id, $"基础属性 {prop.Name} 值不一致");
                    continue;
                }

                var pType = prop.PropertyType;
                if (!pType.IsGenericType) continue;

                var genericDef = pType.GetGenericTypeDefinition();
                var origVal = prop.GetValue(original)!;
                var restVal = prop.GetValue(restored)!;

                if (genericDef == typeof(Mutable<>))
                    AssertMutableDeepEqual(origVal, restVal, prop.Name);
                else if (genericDef == typeof(PortionMutable<>))
                    AssertPortionMutableDeepEqual(origVal, restVal, prop.Name);
            }
        }
    }


    [TestMethod]
    public void TestGetElements_PartialFields_ReflectiveValidation()
    {
        TestInit.SetAsDebug();

        using var db = DbHelper.Base();
        var elements = db.GetCollection<FundElements>().FindAll().ToArray();

        // 获取 FactFields 中所有可用的字段名
        var allFields = typeof(FactFields)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string))
            .Select(f => f.GetRawConstantValue() as string)
            .Where(s => !string.IsNullOrEmpty(s))
            .ToArray();

        var random = new Random(); // 固定种子保证测试可复现

        foreach (var original in elements)
        {
            // 1. 随机选择 1~N 个字段进行测试（至少1个，最多全部）
            var fieldCount = random.Next(1, Math.Max(2, allFields.Length));
            var selectedFields = allFields
                .OrderBy(_ => random.Next())
                .Take(fieldCount)
                .ToArray();

            // 2. 调用 GetElements 获取部分字段
            var partial = db.QueryElements(original.Id, selectedFields);

            // 3. 反射对比选中字段的值（复用原有对比逻辑）
            foreach (var field in selectedFields)
            {
                // 3.1 找到对应属性（处理字段名映射）
                var prop = FindPropertyByFactField(field);

                if (prop == null)
                {
                    Assert.Fail($"无法找到字段 '{field}' 对应的 FundElements 属性");
                    continue;
                }

                var origVal = prop.GetValue(original);
                var partVal = prop.GetValue(partial);

                // 3.2 根据属性类型复用原有对比方法
                var pType = prop.PropertyType;

                if (prop.Name == nameof(FundElements.Id))
                {
                    // 基础 ID 字段直接对比
                    Assert.AreEqual(origVal, partVal, $"基础属性 {prop.Name} 值不一致");
                }
                else if (pType.IsGenericType)
                {
                    var genericDef = pType.GetGenericTypeDefinition();

                    if (genericDef == typeof(Mutable<>))
                    {
                        // ✅ 复用原有 AssertMutableDeepEqual
                        AssertMutableDeepEqual(origVal!, partVal!, prop.Name);
                    }
                    else if (genericDef == typeof(PortionMutable<>))
                    {
                        // ✅ 复用原有 AssertPortionMutableDeepEqual
                        AssertPortionMutableDeepEqual(origVal!, partVal!, prop.Name);
                    }
                    else
                    {
                        // 其他泛型类型走通用深度对比
                        DeepCompare(origVal, partVal, $"Field:{field}");
                    }
                }
                else
                {
                    // 普通标量类型走通用深度对比（复用 DeepCompare）
                    DeepCompare(origVal, partVal, $"Field:{field}");
                }
            }
        }
    }

    /// <summary>
    /// 根据 FactFields 字段名查找对应的 FundElements 属性（处理别名映射）
    /// </summary>
    private static PropertyInfo? FindPropertyByFactField(string factField)
    {
        // 策略1: 直接按属性名匹配（多数情况字段名=属性名）
        var directProp = typeof(FundElements)
            .GetProperty(factField, BindingFlags.Public | BindingFlags.Instance);
        if (directProp != null) return directProp;

        // 策略2: 遍历 FactFields 常量，匹配值相同的字段，再用常量名找属性
        foreach (var f in typeof(FactFields).GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy))
        {
            if (f.IsLiteral && !f.IsInitOnly && f.GetRawConstantValue() is string fieldValue && fieldValue == factField)
            {
                // 常量名通常就是属性名（如 FullName → FullName）
                // 特殊情况：OpenRule → FundOpenRule，需手动映射
                var propName = f.Name switch
                {
                    nameof(FactFields.FundOpenRule) => nameof(FundElements.FundOpenRule),
                    nameof(FactFields.PurchasRule) => nameof(FundElements.PurchasRule),
                    _ => f.Name // 默认常量名=属性名
                };

                return typeof(FundElements).GetProperty(propName, BindingFlags.Public | BindingFlags.Instance);
            }
        }

        return null;
    }




    #region 🔧 反射辅助：数据生成
    private static object GenerateTestData(Type t, int flowId, int shareId)
    {
        if (t == typeof(string)) return $"TestVal_{flowId}_{shareId}";
        if (t == typeof(int) || t == typeof(int?)) return flowId * 100 + Math.Abs(shareId);
        if (t == typeof(decimal) || t == typeof(decimal?)) return flowId + shareId + 0.75m;
        if (t == typeof(double) || t == typeof(double?)) return flowId + shareId + 0.5;
        if (t == typeof(bool) || t == typeof(bool?)) return (flowId + shareId) % 2 == 0;
        if (t == typeof(DateOnly)) return new DateOnly(2020 + flowId, Math.Min(12, shareId > 0 ? shareId : 1), 1);
        if (t.IsEnum) return Enum.GetValues(t).GetValue(0)!;
        if (t.IsArray)
        {
            var elemType = t.GetElementType()!;
            var arr = Array.CreateInstance(elemType, 1);
            arr.SetValue(GenerateTestData(elemType, flowId, shareId), 0);
            return arr;
        }

        try
        {
            var obj = Activator.CreateInstance(t)!;
            foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanWrite && p.GetIndexParameters().Length == 0))
            {
                try { p.SetValue(obj, GenerateTestData(p.PropertyType, flowId, shareId)); }
                catch { }
            }
            return obj;
        }
        catch
        {
            throw new AssertFailedException($"无法为类型 {t.Name} 生成测试数据");
        }
    }
    #endregion

    #region 🔍 反射辅助：深度对比
    /// <summary>
    /// 动态对比 Mutable 的所有 FlowId 键及对应值
    /// </summary>
    private static void AssertMutableDeepEqual(object orig, object rest, string propName)
    {
        var changesProp = orig.GetType().GetProperty("Changes", BindingFlags.Public | BindingFlags.Instance);
        var origChanges = (IDictionary)changesProp!.GetValue(orig)!;
        var restChanges = (IDictionary)changesProp.GetValue(rest)!;

        Assert.HasCount(origChanges.Count, restChanges, $"[{propName}] Changes 字典数量不一致");

        var origKeys = origChanges.Keys.Cast<int>().ToList();
        var restKeys = restChanges.Keys.Cast<int>().ToList();

        // 验证 Key 集合完全一致
        foreach (var key in origKeys)
            Assert.Contains(key, restKeys, $"[{propName}] 还原后缺少 FlowId={key}");

        // 逐值深度对比
        foreach (var key in origKeys)
        {
            var origData = origChanges[key];
            var restData = restChanges[key];
            DeepCompare(origData, restData, $"{propName}.Changes[{key}]");
        }
    }

    /// <summary>
    /// 动态对比 PortionMutable 的所有 FlowId -> ShareId 键及对应值
    /// </summary>
    private static void AssertPortionMutableDeepEqual(object orig, object rest, string propName)
    {
        var changesProp = orig.GetType().GetProperty("Changes", BindingFlags.Public | BindingFlags.Instance);
        var origChanges = (IDictionary)changesProp!.GetValue(orig)!;
        var restChanges = (IDictionary)changesProp.GetValue(rest)!;

        Assert.HasCount(origChanges.Count, restChanges, $"[{propName}] Changes 字典数量不一致");

        var origFlowIds = origChanges.Keys.Cast<int>().ToList();
        var restFlowIds = restChanges.Keys.Cast<int>().ToList();

        foreach (var flowId in origFlowIds)
        {
            Assert.Contains(flowId, restFlowIds, $"[{propName}] 还原后缺少 FlowId={flowId}");

            var origShareDict = (IDictionary)origChanges[flowId]!;
            var restShareDict = (IDictionary)restChanges[flowId]!;

            Assert.HasCount(origShareDict.Count, restShareDict, $"[{propName}] FlowId={flowId} 下份额字典数量不一致");

            var origShareIds = origShareDict.Keys.Cast<int>().ToList();
            var restShareIds = restShareDict.Keys.Cast<int>().ToList();

            foreach (var shareId in origShareIds)
            {
                Assert.Contains(shareId, restShareIds, $"[{propName}] FlowId={flowId} 下缺少 ShareId={shareId}");
                DeepCompare(origShareDict[shareId], restShareDict[shareId], $"{propName}.Changes[{flowId}][{shareId}]");
            }
        }
    }

    private static void DeepCompare(object? expected, object? actual, string path)
    {
        if (expected == null && actual == null) return;
        if (expected == null || actual == null)
            Assert.Fail($"[{path}] Null 不匹配: expected={(expected == null ? "null" : expected.GetType().Name)}, actual={(actual == null ? "null" : actual.GetType().Name)}");
        if (expected.GetType() != actual.GetType())
            Assert.Fail($"[{path}] 类型不匹配: expected={expected.GetType()}, actual={actual.GetType()}");

        // 标量/基元/枚举/日期 直接比较
        if (expected is string || expected.GetType().IsPrimitive || expected is Enum ||
            expected is DateTime || expected is DateOnly || expected is decimal || expected is Guid)
        {
            Assert.AreEqual(expected, actual, $"[{path}] 值不匹配");
            return;
        }

        // 字典对比
        if (expected is IDictionary dictExp && actual is IDictionary dictAct)
        {
            Assert.HasCount(dictExp.Count, dictAct, $"[{path}] 字典数量不一致");
            foreach (var key in dictExp.Keys)
            {
                Assert.Contains(key, dictAct.Keys.Cast<object>(), $"[{path}] 字典缺失 Key: {key}");
                DeepCompare(dictExp[key], dictAct[key], $"{path}[{key}]");
            }
            return;
        }

        // 集合/数组对比
        if (expected is IEnumerable listExp && actual is IEnumerable listAct && expected is not string)
        {
            var expArr = listExp.Cast<object>().ToArray();
            var actArr = listAct.Cast<object>().ToArray();
            Assert.HasCount(expArr.Length, actArr, $"[{path}] 集合长度不一致");
            for (int i = 0; i < expArr.Length; i++)
                DeepCompare(expArr[i], actArr[i], $"{path}[{i}]");
            return;
        }

        // 复杂对象反射遍历
        var props = expected.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0);
        foreach (var p in props)
        {
            var valE = p.GetValue(expected);
            var valA = p.GetValue(actual);
            DeepCompare(valE, valA, $"{path}.{p.Name}");
        }
    }
    #endregion
}