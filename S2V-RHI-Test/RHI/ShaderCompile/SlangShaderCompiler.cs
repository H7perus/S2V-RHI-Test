using System;
using System.Collections.Generic;
using System.Text;

using SlangShaderSharp;

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
    }
}
