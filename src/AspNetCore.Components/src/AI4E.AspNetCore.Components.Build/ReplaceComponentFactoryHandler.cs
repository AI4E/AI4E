using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using AI4E.AspNetCore.Components.Factory;
using Mono.Cecil;
using Mono.Cecil.Cil;

#nullable disable

namespace AI4E.AspNetCore.Components.Build
{
    public class ReplaceComponentFactoryHandler
    {
        private static readonly Type _componentFactoryLoaderType = typeof(ComponentFactoryLoader);
        private static readonly MethodInfo _instantiateComponentMethod = _componentFactoryLoaderType.GetMethod(nameof(ComponentFactoryLoader.InstantiateComponent));

        public ReplaceComponentFactoryHandler() { }

        public string DllFilePath { get; set; }

        public async Task<bool> ExecuteAsync()
        {
            // Load the Microsoft.AspNetCore.Components assembly
            using var componentsAssembly = AssemblyDefinition.ReadAssembly(DllFilePath, new ReaderParameters()
            {
                ReadWrite = true
            });

            var componentsModule = componentsAssembly.MainModule;
            var componentFactoryTypeDefinition = componentsModule.GetType("Microsoft.AspNetCore.Components.ComponentFactory");

            // Add a reference  Microsoft.AspNetCore.Components => AI4E.AspNetCore.Component.Factory.Loader
            var instantiateComponentMethodReference = componentsModule.ImportReference(_instantiateComponentMethod);


            RewriteInstanteComponentMethod(componentsModule, componentFactoryTypeDefinition, instantiateComponentMethodReference);


            //RemoveUnneededMembers(componentFactoryTypeDefinition);
            componentsAssembly.Write();

            return true;
        }

        // Remove all members in the Microsoft.AspNetCore.Components.ComponentFactory type exept for:
        // - Instance 
        // - InstantiateComponent
        private static void RemoveUnneededMembers(TypeDefinition componentFactoryTypeDefinition)
        {


            componentFactoryTypeDefinition.Fields.Clear();

            foreach (var property in componentFactoryTypeDefinition.Properties.ToArray())
            {
                if (property.Name == "Instance")
                    continue;

                componentFactoryTypeDefinition.Properties.Remove(property);
            }

            foreach (var method in componentFactoryTypeDefinition.Methods.ToArray())
            {
                if (method.Name == ".ctor" || method.Name == "InstantiateComponent")
                    continue;

                componentFactoryTypeDefinition.Methods.Remove(method);
            }
        }

        // Replace the content of the Microsoft.AspNetCore.Components.ComponentFactory.InstantiateComponent method with
        //
        // [1] var instance = AI4E.AspNetCore.Component.Factory.Loader.ComponentFactoryLoader.InstantiateComponent(serviceProvider, componentType);
        // [2] if (!(instance is IComponent component))
        // [3] {
        // [4]    throw new ArgumentException($"The type {componentType.FullName} does not implement {nameof(IComponent)}.", nameof(componentType));
        // [5] }
        // [6]
        // [7] return component;
        //
        private static TypeDefinition RewriteInstanteComponentMethod(
            ModuleDefinition componentsModule,
            TypeDefinition componentFactoryTypeDefinition,
            MethodReference instantiateComponentMethodReference)
        {
            var voidType = componentsModule.TypeSystem.Void;
            var objectType = componentsModule.TypeSystem.Object;
            var boolType = componentsModule.TypeSystem.Boolean;
            var stringType = componentsModule.TypeSystem.String;
            var componentType = componentsModule.GetType("Microsoft.AspNetCore.Components.IComponent");
            var typeType = new TypeReference("System", "Type", componentsModule, componentsModule.TypeSystem.CoreLibrary);
            var typeGetFullNameMethod = new MethodReference(
                "get_FullName",
                returnType: stringType,
                declaringType: typeType)
            {
                HasThis = true
            };
            var stringConcat3Method = new MethodReference(
                "Concat",
                returnType: stringType,
                declaringType: stringType)
            {
                HasThis = false
            };
            stringConcat3Method.Parameters.Add(new ParameterDefinition(stringType));
            stringConcat3Method.Parameters.Add(new ParameterDefinition(stringType));
            stringConcat3Method.Parameters.Add(new ParameterDefinition(stringType));

            var argumentExceptionType = new TypeReference("System", "ArgumentException", componentsModule, componentsModule.TypeSystem.CoreLibrary);
            var argumentExceptionCtor = new MethodReference(
                ".ctor",
                returnType: voidType,
                declaringType: argumentExceptionType)
            {
                HasThis = true
            };

            argumentExceptionCtor.Parameters.Add(new ParameterDefinition(stringType));
            argumentExceptionCtor.Parameters.Add(new ParameterDefinition(stringType));

            var instantiateComponentMethodDefinition = componentFactoryTypeDefinition.Methods.First(p => p.Name == "InstantiateComponent");
            var body = instantiateComponentMethodDefinition.Body;

            body.Instructions.Clear();
            body.MaxStackSize = 3;
            body.InitLocals = true;
            body.Variables.Clear();
            body.Variables.Add(new VariableDefinition(objectType)); // 'instance'
            body.Variables.Add(new VariableDefinition(componentType)); // component
            body.Variables.Add(new VariableDefinition(boolType));
            body.Variables.Add(new VariableDefinition(componentType));

            var ilProcessor = body.GetILProcessor();

            var IL_003c = ilProcessor.Create(OpCodes.Ldloc_1);
            var IL_0040 = ilProcessor.Create(OpCodes.Ldloc_3);

            // [0]
            ilProcessor.Emit(OpCodes.Nop);

            // [1]
            ilProcessor.Emit(OpCodes.Ldarg_1); // serviceProvider
            ilProcessor.Emit(OpCodes.Ldarg_2); // componentType
            ilProcessor.Emit(OpCodes.Call, instantiateComponentMethodReference);
            ilProcessor.Emit(OpCodes.Stloc_0); // 'instance'

            // [2]
            ilProcessor.Emit(OpCodes.Ldloc_0); // 'instance'
            ilProcessor.Emit(OpCodes.Isinst, componentType);
            ilProcessor.Emit(OpCodes.Stloc_1); // component
            ilProcessor.Emit(OpCodes.Ldloc_1); // component
            ilProcessor.Emit(OpCodes.Ldnull);
            ilProcessor.Emit(OpCodes.Cgt_Un);
            ilProcessor.Emit(OpCodes.Ldc_I4_0);
            ilProcessor.Emit(OpCodes.Ceq);
            ilProcessor.Emit(OpCodes.Stloc_2);

            ilProcessor.Emit(OpCodes.Ldloc_2);
            ilProcessor.Emit(OpCodes.Brfalse_S, IL_003c);

            // [3]
            ilProcessor.Emit(OpCodes.Nop);

            // [4]
            ilProcessor.Emit(OpCodes.Ldstr, "The type ");
            ilProcessor.Emit(OpCodes.Ldarg_2); // componentType
            ilProcessor.Emit(OpCodes.Callvirt, typeGetFullNameMethod);
            ilProcessor.Emit(OpCodes.Ldstr, " does not implement IComponent.");
            ilProcessor.Emit(OpCodes.Call, stringConcat3Method);
            ilProcessor.Emit(OpCodes.Ldstr, "componentType");
            ilProcessor.Emit(OpCodes.Newobj, argumentExceptionCtor);
            ilProcessor.Emit(OpCodes.Throw);

            // [7]
            ilProcessor.Append(IL_003c); // component
            ilProcessor.Emit(OpCodes.Stloc_3);
            ilProcessor.Emit(OpCodes.Br_S, IL_0040);

            // [8]
            ilProcessor.Append(IL_0040);
            ilProcessor.Emit(OpCodes.Ret);

            return componentFactoryTypeDefinition;
        }
    }
}
