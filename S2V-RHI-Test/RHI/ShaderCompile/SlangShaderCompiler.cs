using SlangShaderSharp;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using Vortice.Vulkan;
using static System.Collections.Specialized.BitVector32;

namespace S2V_RHI_Test.RHI.ShaderCompile
{
    public class SlangShaderCompiler
    {
        IGlobalSession slangGlobalSession;
        ISession slangSession;
        public SlangShaderCompiler()
        {
            Slang.CreateGlobalSession(Slang.ApiVersion, out slangGlobalSession);

            var sessionDesc = new SessionDesc
            {
                Targets = [new TargetDesc 
                {   Format = SlangCompileTarget.Spirv,
                    Profile = slangGlobalSession.FindProfile("spirv_1_6"),

                }],
                DefaultMatrixLayoutMode = SlangMatrixLayoutMode.ColumnMajor,
                CompilerOptionEntries = [
                    new CompilerOptionEntry { Name = CompilerOptionName.BindlessSpaceIndex, Value = CompilerOptionValue.FromInt(1)},
                    //H7per: could change this to 3 later
                    new CompilerOptionEntry { Name = CompilerOptionName.Optimization, Value = CompilerOptionValue.FromInt(2) }
                    ]
            };

            slangGlobalSession.CreateSession(sessionDesc, out slangSession);

        }

        //FOR TESTING
        public unsafe byte[] Compile(string shader)
        {
            var module = slangSession.LoadModuleFromSourceString("test", "../../../Shaders/testShaderDescriptorHandle.slang", shader, out var diagnostics);

            //var error = diagnostics.AsString;

            var layout = module.GetLayout(0, out var diagnosticsLayout);

            var fieldCount = layout.ParameterCount;

            for (uint i = 0; i < fieldCount; i++)
            {
                var field = layout.GetParameterByIndex(i);

                var name = field.Name;
                var typeName = field.Type.Name;

                var typeLayoutName = field.TypeLayout.Name;

                var pushConstantElement = field.TypeLayout.ElementTypeLayout.GetFieldByIndex(0);

                var subName = pushConstantElement.TypeLayout.ContainerVarLayout.Name;

                var descriptorHandleGenericContainer = pushConstantElement.Type.GenericContainer;

                var descriptorHandleType = descriptorHandleGenericContainer.GetConcreteType(descriptorHandleGenericContainer.GetTypeParameter(0));


                Console.WriteLine("hello");
            }

            module.GetTargetCode(0, out var code, out var diagnostics2);

            var span = new Span<byte>(code.GetBufferPointer(), (int)code.GetBufferSize());
            return span.ToArray();
        }

        public SlangShaderModule LoadShaderModule(string name)
        {
            var module = slangSession.LoadModule(name, out var loadDiagnostics);

            List<CompileTimeConstant> compileTimeConstants = new();

            var fileCount = module.GetDependencyFileCount();

            for (int i = 0; i < fileCount; i++)
            {
                var depModuleReflection = slangSession.LoadModule(module.GetDependencyFilePath(i), out var diagDependency)!.GetModuleReflection();

                for (uint child = 0; child < depModuleReflection.ChildrenCount; child++)
                {
                    var param = depModuleReflection.GetChild(child);

                    var asVar = param.AsVariable();

                    if (param.Kind == DeclReflectionKind.Variable && asVar.FindModifier(ModifierID.Extern)! != 0)
                    {
                        int min = -1, max = -1;

                        asVar.GetDefaultValueBlob(out var defBlob);

                        int def = BitConverter.ToInt32(defBlob.Buffer);


                        for (var attribIndex = 0; attribIndex < asVar.AttributeCount; attribIndex++)
                        {
                            var attributeRefl = asVar.GetAttribute((uint)attribIndex);
                            if (attributeRefl.Name == "ConstantRange")
                            {
                                min = attributeRefl.GetArgumentValueInt(0);
                                max = attributeRefl.GetArgumentValueInt(1);
                            }
                        }
                        

                        compileTimeConstants.Add(new() { Name = param.Name, Min = min, Max = max, Default = def });
                    }
                }

            }

            compileTimeConstants.Sort((x, y) => String.Compare(x.Name, y.Name));

            return new SlangShaderModule(module, compileTimeConstants);
        }

        public SpecialisedShader SpecialiseAndCompile(SlangShaderModule shaderModule, IReadOnlyDictionary<string, int> arguments = null)
        {
            //guh, thats wild. Need sorting so we can traverse the arguments more easily.
            var constantValues = (from entry in arguments orderby entry.Key ascending select entry).AsEnumerable();

            var defaultCompileTimeConstantValues = shaderModule.CompileTimeConstants.Select(a => new CompileTimeConstantValue
            {
                // map shared properties
                Name = a.Name,
                Value = a.Default,

            }).ToArray();

            int defaultsIndex = 0;
            int passedConstantsIndex = 0;


            //totally not C and two plusses in a trench coat
            while (defaultsIndex < defaultCompileTimeConstantValues.Count() &&
                   passedConstantsIndex < constantValues.Count())
            {
                if (constantValues.ElementAt(passedConstantsIndex).Key == defaultCompileTimeConstantValues[defaultsIndex].Name)
                {
                    var val = constantValues.ElementAt(passedConstantsIndex).Value;
                    var min = shaderModule.CompileTimeConstants[defaultsIndex].Min;
                    var max = shaderModule.CompileTimeConstants[defaultsIndex].Max;
                    int clamped = Math.Clamp(val, min, max);

                    if (val != clamped)
                    {
                        Console.WriteLine("Shader Specialisation: passed constant Value not in range!"); 
                        // we would throw a warning here;
                    }

                    defaultCompileTimeConstantValues[defaultsIndex].Value = clamped;

                    passedConstantsIndex++;
                }
                defaultsIndex++;


                if (defaultsIndex + 1 == defaultCompileTimeConstantValues.Length &&
                    passedConstantsIndex < constantValues.Count())
                {
                    //throw std::logic_error("Passed constant(s) have no equivalent in the shader!");
                }
            }



            string specialisationModuleString = "";

            if (arguments != null)
                foreach (var argument in defaultCompileTimeConstantValues)
                {
                    specialisationModuleString += $"export static const int {argument.Name} = {argument.Value}; \n";
                }

            shaderModule.Module.Link(out var linkedComponent, out var diagLink);

            var specialisationModule = 
                slangSession.LoadModuleFromSourceString(
                specialisationModuleString, 
                specialisationModuleString, 
                specialisationModuleString, 
                out var specialisationModulediagnostics);

            var entryPointCount = shaderModule.Module.GetDefinedEntryPointCount();

            Span<IComponentType> components = new IComponentType[2 + entryPointCount];

            components[0] = linkedComponent;
            components[1] = specialisationModule;

            for (var i = 0; i < entryPointCount; i++)
            {
                shaderModule.Module.GetDefinedEntryPoint(i, out var entry);
                components[2 + i] = entry;
            }


            slangSession.CreateCompositeComponentType(components, out var composedProgram, out var specialisationDiagnostics);

            var composedLayout = composedProgram.GetLayout(0, out var diagCompLayout);
            var moduleLayout = shaderModule.Module.GetLayout(0, out var diagModLayout);

            composedProgram.GetTargetCode(0, out var code, out var diagCode);

           

            //is allocating new bad here?
            Memory<byte> spirv = code.Buffer.ToArray();
            Dictionary<VkShaderStageFlags, string> stages = new();
            List<VertexInput> vertexInputs = new();
            List<StructMember> pushConstants = new();
            List<StructMember> parameters = new();

            List<StructMember> compileTimeConstants = new();

            var entryPointVertex = composedLayout.GetEntryPointByIndex(0);


            VkShaderStageFlags GetVkStageFlag(SlangStage stage)
            {
                switch (stage) {
                    case SlangStage.Vertex:
                        return VkShaderStageFlags.Vertex;
                    case SlangStage.Fragment:
                        return VkShaderStageFlags.Fragment;
                    case SlangStage.Compute:
                        return VkShaderStageFlags.Compute;
                    case SlangStage.Mesh:
                        return VkShaderStageFlags.MeshEXT;
                    default:
                        return 0;
                }
            }

            for (uint i = 0; i < composedLayout.EntryPointCount; i++)
            {
                var entryReflection = composedLayout.GetEntryPointByIndex(i);

                stages.Add(GetVkStageFlag(entryReflection.Stage), entryReflection.Name);

                if (entryReflection.Stage == SlangStage.Vertex)
                {
                    vertexInputs = ReflectVertexInputs(entryReflection);
                }
            }

            bool pushConstantBlockFound = false;
            bool parametersFound = false;

            for (uint i = 0; i < composedLayout.ParameterCount; i++)
            {
                var param = composedLayout.GetParameterByIndex(i);

                if (param.Category == SlangParameterCategory.PushConstantBuffer)
                {
                    if (pushConstantBlockFound == true)
                    {
                        throw new Exception("More than one push constant block found!");
                    }
                    pushConstantBlockFound = true;


                    var pushConstantFieldCount = param.TypeLayout.ElementTypeLayout.FieldCount;

                    for (uint pushElement = 0; pushElement < pushConstantFieldCount; pushElement++)
                    {



                        var element = param.TypeLayout.ElementTypeLayout.GetFieldByIndex(pushElement);

                        string name = element.Name;
                        string type = GetTypeString(element.Type);

                        //hardcoded, it makes life easier
                        if (type == "DescriptorHandle<ConstantBuffer<Parameters>>")
                        {
                            if (parametersFound == true)
                            {
                                throw new Exception("More than one parameter block found!");
                            }

                            var outerGenericContainer = element.Type.GenericContainer;

                            var innerGenericContainer = outerGenericContainer.GetConcreteType(outerGenericContainer.GetTypeParameter(0)).GenericContainer;

                            var parameterStruct = innerGenericContainer.GetConcreteType(innerGenericContainer.GetTypeParameter(0));


                            // this better work
                            var parameterStructTypeLayout = slangSession.GetTypeLayout(parameterStruct, 0, LayoutRules.DefaultConstantBuffer, out var typeLayoutDiag);

                            var parameterCount = parameterStructTypeLayout.FieldCount;

                            for (uint parameterIndex = 0; parameterIndex < (uint)parameterCount; parameterIndex++)
                            {
                                var parameter = parameterStructTypeLayout.GetFieldByIndex(parameterIndex);


                                parameters.Add(new()
                                {
                                    Name = parameter.Name,
                                    Type = GetTypeString(parameter.Type),
                                    Offset = (uint)parameter.GetOffset()
                                });
                            }

                            parametersFound = true;
                        }

                        uint offset = (uint)element.GetOffset();

                        var elementType = element.Type;

                        var genericContainer = elementType.GenericContainer;

                        pushConstants.Add(new() { Name = name, Type = type, Offset = offset });

                    }
                }

            }



            return new SpecialisedShader(
    Spirv: spirv,
    Stages: stages,
    VertexInputs: vertexInputs,
    PushConstants: pushConstants,
    MaterialParameters: parameters,
    CompileTimeConstantValues: defaultCompileTimeConstantValues);
        }


        private List<VertexInput> ReflectVertexInputs(EntryPointReflection vertexStageReflection)
        {
            var vertexInputs = new List<VertexInput>();

            for (uint i = 0; i < vertexStageReflection.ParameterCount; i++)
            {
                var param = vertexStageReflection.GetParameterByIndex(i);

                if (param.Category == SlangParameterCategory.VaryingInput)
                    ReflectVertexParameter(param, ref vertexInputs);
            }

            return vertexInputs;
        }

        private void ReflectVertexParameter(VariableLayoutReflection vertexInputReflection, ref List<VertexInput> vertexInputs)
        {
            if (vertexInputReflection.TypeLayout.Kind == SlangTypeKind.Struct)
            {
                for (uint iElement = 0; iElement < vertexInputReflection.TypeLayout.FieldCount; iElement++)
                {
                    var element = vertexInputReflection.TypeLayout.GetFieldByIndex(iElement);

                    if (element.Category == SlangParameterCategory.VaryingInput)
                    {
                        ReflectVertexParameter(element, ref vertexInputs);
                    }
                }
            }
            else
            {
            VkFormat vkFormat;
            uint elementSize = 0;
            switch (vertexInputReflection.TypeLayout.ScalarType)
            {
                case SlangScalarType.Float32:
                    vkFormat = VkFormat.R32Sfloat;
                    elementSize = 4;
                    break;
                case SlangScalarType.Int32:
                    vkFormat = VkFormat.R32Sint;
                    elementSize = 4;
                    break;
                case SlangScalarType.UInt32:
                    vkFormat = VkFormat.R32Uint;
                    elementSize = 4;
                    break;
                default:
                    vkFormat = 0;
                    break;
            }

            var elementCount = Math.Max((uint)vertexInputReflection.TypeLayout.ColumnCount, 1);
            //a bit evil but this enum is static, so this trick won't just break with a new Vulkan version or anything.
            vkFormat = (VkFormat)((uint)vkFormat + (elementCount - 1) * 3);


                vertexInputs.Add(new VertexInput()
                {
                    SemanticName = vertexInputReflection.SemanticName,
                    SemanticIndex = (uint)vertexInputReflection.SemanticIndex,
                    Format = vkFormat,
                    Location = vertexInputReflection.BindingIndex,
                    Size = elementCount * elementSize
                });
            }
            
            
        }

        private string GetTypeString(TypeReflection typeReflection)
        {
            var genericContainer = typeReflection.GenericContainer;

            if (genericContainer.TypeParameterCount != 0 && (typeReflection.Kind == SlangTypeKind.Struct || typeReflection.Kind == SlangTypeKind.ConstantBuffer))
            {
                return typeReflection.Name + $"<{GetTypeString(genericContainer.GetConcreteType(genericContainer.GetTypeParameter(0)))}>";
            }
            else
            {
                if (typeReflection.Kind == SlangTypeKind.Vector)
                {
                    return typeReflection.ElementType.Name + $"{typeReflection.ColumnCount}";
                }
                if (typeReflection.Kind == SlangTypeKind.Array)
                {
                    return GetTypeString(typeReflection.ElementType) + $"[{typeReflection.ElementCount}]";
                }
                if (typeReflection.Kind == SlangTypeKind.Matrix)
                {
                    return GetTypeString(typeReflection.ElementType) + $"{typeReflection.RowCount}x" + $"{typeReflection.ColumnCount}";
                }
                return typeReflection.Name;
            }
        }
    }
}
