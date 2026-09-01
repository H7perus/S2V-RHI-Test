module Core;


//A war crime that should be a compiler option instead of this bullshit
export T getDescriptorFromHandle<T>(DescriptorHandle<T> handle) where T : IOpaqueDescriptor
{
    return defaultGetDescriptorFromHandle(handle, BindlessDescriptorOptions.None);
}

[__AttributeUsage(_AttributeTargets.Var)]
struct ConstantRange
{
    int min;
    int max;
};

public extern static const int testFromAnotherModuleBuh = 0;
public extern static const int testFromAnotherModuleBuh3 = 0;
public extern static const int testFromAnotherModuleBuh4 = 0;
public extern static const int testFromAnotherModuleBuh5 = 0;

enum TestEnum
{
    Type1,
    Type2
};