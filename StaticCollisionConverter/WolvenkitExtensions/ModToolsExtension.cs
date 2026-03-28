using System.Numerics;
using System.Reflection;
using SharpGLTF.Schema2;
using StaticCollisionConverter.Services;
using WolvenKit.Common.Model.Arguments;
using WolvenKit.Core.Interfaces;
using WolvenKit.Modkit.RED4;
using WolvenKit.Modkit.RED4.GeneralStructs;
using WolvenKit.Modkit.RED4.RigFile;
using WolvenKit.Modkit.RED4.Tools;
using WolvenKit.RED4.Archive.CR2W;
using WolvenKit.RED4.Types;

namespace StaticCollisionConverter.WolvenKitExtensions;

public class ModToolsExtension
{
  private WolvenKitWrapper _wkit;
  private ModTools _modTools;
  private ILoggerService _loggerService;
  
  private MethodInfo? _verifyGLTF;
  private MethodInfo? _gltfMeshToRawContainer;
  private MethodInfo? _createEmptyMesh;
  private MethodInfo? _bufferWriter;
  private MethodInfo? _rawMeshToRE4Mesh;
  private MethodInfo? _getEditedCr2wFile;

  public ModToolsExtension(WolvenKitWrapper wkit)
  {
    this._wkit = wkit;
    this._modTools = wkit.ModTools;
    this._loggerService = wkit.LoggerService;
    
    _verifyGLTF = typeof(ModTools)
      .GetMethod("VerifyGLTF", BindingFlags.NonPublic | BindingFlags.Static);
    
    _gltfMeshToRawContainer =
      typeof(ModTools).GetMethod(
        "GltfMeshToRawContainer",
        BindingFlags.NonPublic | BindingFlags.Instance,
        null,
        [typeof(Node), typeof(GltfImportArgs)],
        null);
    
    _createEmptyMesh = typeof(ModTools)
      .GetMethod("CreateEmptyMesh", BindingFlags.NonPublic | BindingFlags.Static);
    
    _bufferWriter = typeof(ModTools)
      .GetMethod("BufferWriter", BindingFlags.NonPublic | BindingFlags.Static);
    
    _rawMeshToRE4Mesh = typeof(ModTools)
      .GetMethod("RawMeshToRE4Mesh", BindingFlags.NonPublic | BindingFlags.Static);
    
    _getEditedCr2wFile = typeof(ModTools)
      .GetMethod("GetEditedCr2wFile", BindingFlags.NonPublic | BindingFlags.Instance);
    
    ArgumentNullException.ThrowIfNull(wkit, nameof(wkit));
    ArgumentNullException.ThrowIfNull(wkit.ModTools, nameof(wkit.ModTools));
    ArgumentNullException.ThrowIfNull(wkit.LoggerService, nameof(wkit.LoggerService));
    
    ArgumentNullException.ThrowIfNull(_verifyGLTF, nameof(_verifyGLTF));
    ArgumentNullException.ThrowIfNull(_gltfMeshToRawContainer, nameof(_gltfMeshToRawContainer));
    ArgumentNullException.ThrowIfNull(_createEmptyMesh, nameof(_createEmptyMesh));
    ArgumentNullException.ThrowIfNull(_bufferWriter, nameof(_bufferWriter));
    ArgumentNullException.ThrowIfNull(_rawMeshToRE4Mesh, nameof(_rawMeshToRE4Mesh));
    ArgumentNullException.ThrowIfNull(_getEditedCr2wFile, nameof(_getEditedCr2wFile));
  }
  
  private void VerifyGLTF(ModelRoot mr, GltfImportArgs args) => _verifyGLTF?.Invoke(_modTools, [mr, args]);
  private RawMeshContainer GltfMeshToRawContainer(Node logicalNode, GltfImportArgs args) => 
          (RawMeshContainer) _gltfMeshToRawContainer?.Invoke(_modTools, [logicalNode, args]);
  private RawMeshContainer CreateEmptyMesh(string name) => (RawMeshContainer) _createEmptyMesh?.Invoke(_modTools, [name]);
  private MeshesInfo BufferWriter(List<Re4MeshContainer> expMeshes, ref MemoryStream ms, GltfImportArgs args) => 
          (MeshesInfo) _bufferWriter?.Invoke(_modTools, [expMeshes, ms, args]);
  private Re4MeshContainer RawMeshToRE4Mesh(RawMeshContainer rawMeshContainer, System.Numerics.Vector4 quantScale, System.Numerics.Vector4 quantTrans) => 
          (Re4MeshContainer) _rawMeshToRE4Mesh?.Invoke(_modTools, [rawMeshContainer, quantScale, quantTrans]);
  private MemoryStream GetEditedCr2wFile(CR2WFile cr2w, MeshesInfo info, MemoryStream buffer) => 
          (MemoryStream) _getEditedCr2wFile?.Invoke(_modTools, [cr2w, info, buffer, null, null]);
  
  public CR2WFile? ImportMesh(ArraySegment<byte> glb, CR2WFile cr2w, GltfImportArgs args)
  {
    if (cr2w is not
        {
          RootChunk: CMesh
          {
            RenderResourceBlob:
            {
              Chunk: rendRenderMeshBlob chunk
            }
          } rootChunk
        })
      return null;
    
    chunk.Header.OpacityMicromaps.Clear();
    
    ModelRoot model = ModelRoot.ParseGLB(glb, new ReadSettings((ReadSettings) args.ValidationMode));
    VerifyGLTF(model, args);
    List<RawMeshContainer> source = new List<RawMeshContainer>();
    foreach (Node logicalNode in (IEnumerable<Node>) model.LogicalNodes)
    {
      if (logicalNode.Mesh != null)
        source.Add(GltfMeshToRawContainer(logicalNode, args));
      else if (args.FillEmpty)
        source.Add(CreateEmptyMesh(logicalNode.Name));
    }
    List<RawMeshContainer> list1 = source.OrderBy<RawMeshContainer, string>((Func<RawMeshContainer, string>) (mesh => mesh.name)).ToList<RawMeshContainer>();
    System.Numerics.Vector3 vector3_1 = new System.Numerics.Vector3(float.MinValue, float.MinValue, float.MinValue);
    System.Numerics.Vector3 vector3_2 = new System.Numerics.Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
    foreach (RawMeshContainer rawMeshContainer in list1)
    {
      ArgumentNullException.ThrowIfNull((object) rawMeshContainer.positions, "p.positions");
      foreach (System.Numerics.Vector3 vector3_3 in ((IEnumerable<System.Numerics.Vector3>) rawMeshContainer.positions).ToList<System.Numerics.Vector3>())
      {
        vector3_1.X = Math.Max(vector3_3.X, vector3_1.X);
        vector3_1.Y = Math.Max(vector3_3.Y, vector3_1.Y);
        vector3_1.Z = Math.Max(vector3_3.Z, vector3_1.Z);
      }
    }
    foreach (RawMeshContainer rawMeshContainer in list1)
    {
      ArgumentNullException.ThrowIfNull((object) rawMeshContainer.positions, "p.positions");
      foreach (System.Numerics.Vector3 vector3_4 in ((IEnumerable<System.Numerics.Vector3>) rawMeshContainer.positions).ToList<System.Numerics.Vector3>())
      {
        vector3_2.X = Math.Min(vector3_4.X, vector3_2.X);
        vector3_2.Y = Math.Min(vector3_4.Y, vector3_2.Y);
        vector3_2.Z = Math.Min(vector3_4.Z, vector3_2.Z);
      }
    }
    rootChunk.BoundingBox.Min = new WolvenKit.RED4.Types.Vector4()
    {
      X = (CFloat) vector3_2.X,
      Y = (CFloat) vector3_2.Y,
      Z = (CFloat) vector3_2.Z,
      W = (CFloat) 1f
    };
    rootChunk.BoundingBox.Max = new WolvenKit.RED4.Types.Vector4()
    {
      X = (CFloat) vector3_1.X,
      Y = (CFloat) vector3_1.Y,
      Z = (CFloat) vector3_1.Z,
      W = (CFloat) 1f
    };
    System.Numerics.Vector4 quantScale = new System.Numerics.Vector4((float) (((double) vector3_1.X - (double) vector3_2.X) / 2.0), (float) (((double) vector3_1.Y - (double) vector3_2.Y) / 2.0), (float) (((double) vector3_1.Z - (double) vector3_2.Z) / 2.0), 0.0f);
    System.Numerics.Vector4 quantTrans = new System.Numerics.Vector4((float) (((double) vector3_1.X + (double) vector3_2.X) / 2.0), (float) (((double) vector3_1.Y + (double) vector3_2.Y) / 2.0), (float) (((double) vector3_1.Z + (double) vector3_2.Z) / 2.0), 1f);
    
    List<Re4MeshContainer> list2 = list1.Select<RawMeshContainer, Re4MeshContainer>((Func<RawMeshContainer, Re4MeshContainer>) (_ => RawMeshToRE4Mesh(_, quantScale, quantTrans))).ToList<Re4MeshContainer>();
    MemoryStream buffer = new MemoryStream();
    ref MemoryStream local = ref buffer;
    GltfImportArgs args1 = args;
    MeshesInfo info = BufferWriter(list2, ref local, args1);
    info.quantScale = quantScale;
    info.quantTrans = quantTrans;
    var editedCr2wFile = GetEditedCr2wFile(cr2w, info, buffer);
    editedCr2wFile.Seek(0L, SeekOrigin.Begin);
    return _wkit.Red4ParserService.ReadRed4File(editedCr2wFile);
  }
}