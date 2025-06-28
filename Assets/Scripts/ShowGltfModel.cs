using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.XR.PXR;
using Unity.XR.PXR.SecureMR;
public class ShowGltfModel : MonoBehaviour
{
    private Provider provider;
    private Pipeline pipeline;
    int image_width = 3248;   // Same as VST_IMAGE_WIDTH
    int image_height = 2464;  // Same as VST_IMAGE_HEIGHT
    private Tensor debugGltfPlaceholder;
    public TextAsset tvGltf;
    private Tensor debugGltfTensor;
    
    // Start is called before the first frame update
    void Start()
    {
        PXR_Manager.EnableVideoSeeThrough = true;
        
        provider = new Provider(image_width, image_height);

        CreateRender();
    }
    
    void CreateRender()
    {
        pipeline = provider.CreatePipeline();
        
        var renderGltfOp = pipeline.CreateOperator<SwitchGltfRenderStatusOperator>();
        var poseMat  = pipeline.CreateTensor<float,Matrix>(1, new TensorShape(4,4));
        
        debugGltfPlaceholder = pipeline.CreateTensorReference<Gltf>();
        var gltfData = tvGltf.bytes;
        debugGltfTensor  = provider.CreateTensor<Gltf>(gltfData);

        renderGltfOp.SetOperand("gltf",debugGltfPlaceholder);
        renderGltfOp.SetOperand("world pose",poseMat);
        float[] poseMatValue = 
        {0.5f, 0.0f, 0.0f, -0.5f,
            0.0f, 0.5f, 0.0f, 0.0f,
            0.0f, 0.0f, 0.5f, -1.5f,
            0.0f, 0.0f, 0.0f, 1.0f};
        poseMat.Reset(poseMatValue);
        
        InvokeRepeating(nameof(RenderFrame), 0, 0.02f);
    }
    
    void RenderFrame()
    {
        var pipelineIOPair = pipeline.CreateTensorMapping();
        pipelineIOPair.Set(debugGltfPlaceholder,debugGltfTensor);
        pipeline.Execute(pipelineIOPair);
    }
}
