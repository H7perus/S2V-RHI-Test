


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