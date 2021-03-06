﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
using ConnectorGrasshopper.Extras;
using ConnectorGrasshopper.Objects;
using GH_IO.Serialization;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;
using Grasshopper.Kernel.Special;
using Rhino.Geometry;
using Speckle.Core.Api;
using Speckle.Core.Kits;
using Speckle.Core.Logging;
using Speckle.Core.Models;

namespace ConnectorGrasshopper
{
  public class CreateSchemaObject : SelectKitComponentBase, IGH_VariableParameterComponent
  {
    private ConstructorInfo SelectedConstructor;
    private GH_Document _document;

    public override Guid ComponentGuid => new Guid("4dc285e3-810d-47db-bfb5-cd96fe459fdd");
    protected override Bitmap Icon => Properties.Resources.SchemaBuilder;

    public override GH_Exposure Exposure => GH_Exposure.primary;

    public string Seed;

    public CreateSchemaObject()
      : base("Create Schema Object", "CsO",
          "Allows you to create a Speckle object from a schema class.",
          "Speckle 2", "Object Management")
    {
      Kit = KitManager.GetDefaultKit();
      try
      {
        Converter = Kit.LoadConverter(Applications.Rhino);
        Message = $"{Kit.Name} Kit";
      }
      catch
      {
        AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "No default kit found on this machine.");
      }

      Seed = GenerateSeed();
    }

    public string GenerateSeed()
    {
      return new string(Speckle.Core.Models.Utilities.hashString(Guid.NewGuid().ToString()).Take(20).ToArray());
    }

    public override void AddedToDocument(GH_Document document)
    {
      if (SelectedConstructor != null)
      {
        base.AddedToDocument(document);
        if (Grasshopper.Instances.ActiveCanvas.Document != null)
        {
          var otherSchemaBuilders = Grasshopper.Instances.ActiveCanvas.Document.FindObjects(new List<string>() { Name }, 10000);
          foreach (var comp in otherSchemaBuilders)
          {
            if (comp is CreateSchemaObject scb)
            {
              if (scb.Seed == Seed)
              {
                Seed = GenerateSeed();
                break;
              }
            }
          }
        }
        return;
      }

      _document = document;

      var dialog = new CreateSchemaObjectDialog();
      dialog.Owner = Grasshopper.Instances.EtoDocumentEditor;
      var viewport = Grasshopper.Instances.ActiveCanvas.Viewport;
      var mouse = GH_Canvas.MousePosition;
      dialog.Location = new Eto.Drawing.Point((int)viewport.MidPoint.X,(int)viewport.MidPoint.Y ); //approx the dialog half-size
      
      dialog.ShowModal();

      if (dialog.HasResult)
      {
        base.AddedToDocument(document);
        SwitchConstructor(dialog.model.SelectedItem.Tag as ConstructorInfo);
      }
      else
      {
        document.RemoveObject(this.Attributes, true);
      }
    }

    public void SwitchConstructor(ConstructorInfo constructor)
    {
      int k = 0;
      var props = constructor.GetParameters();

      foreach (var p in props)
      {
        RegisterPropertyAsInputParameter(p, k++);
      }

      this.Name = constructor.GetCustomAttribute<SchemaInfo>().Name;
      this.Description = constructor.GetCustomAttribute<SchemaInfo>().Description;

      Message = constructor.DeclaringType.FullName.Split('.')[0];
      SelectedConstructor = constructor;
      Params.Output[0].NickName = constructor.DeclaringType.Name;
      Params.OnParametersChanged();
      ExpireSolution(true);
    }

    /// <summary>
    /// Adds a property to the component's inputs.
    /// </summary>
    /// <param name="param"></param>
    private void RegisterPropertyAsInputParameter(ParameterInfo param, int index)
    {
      // get property name and value
      Type propType = param.ParameterType;

      string propName = param.Name;
      object propValue = param;

      var inputDesc = param.GetCustomAttribute<SchemaParamInfo>();
      var d = inputDesc != null ? inputDesc.Description : "";
      if (param.IsOptional)
      {
        if (!string.IsNullOrEmpty(d))
          d += ", ";
        var def = param.DefaultValue == null ? "null" : param.DefaultValue.ToString();
        d += "default = " + def;
      }

      // Create new param based on property name
      Param_GenericObject newInputParam = new Param_GenericObject();
      newInputParam.Name = propName;
      newInputParam.NickName = propName;
      newInputParam.MutableNickName = false;

      newInputParam.Description = $"({propType.Name}) {d}";
      newInputParam.Optional = param.IsOptional;

      // check if input needs to be a list or item access
      bool isCollection = typeof(System.Collections.IEnumerable).IsAssignableFrom(propType) && propType != typeof(string) && !propType.Name.ToLower().Contains("dictionary");
      if (isCollection == true)
      {
        newInputParam.Access = GH_ParamAccess.list;
      }
      else
      {
        newInputParam.Access = GH_ParamAccess.item;
      }
      Params.RegisterInputParam(newInputParam, index);

      //add dropdown
      if (propType.IsEnum)
      {
        //expire solution so that node gets proper size
        ExpireSolution(true);

        var instance = Activator.CreateInstance(propType);

        var vals = Enum.GetValues(propType).Cast<Enum>().Select(x => x.ToString()).ToList();
        var options = CreateDropDown(propName, vals, Attributes.Bounds.X, Params.Input[index].Attributes.Bounds.Y);
        _document.AddObject(options, false);
        Params.Input[index].AddSource(options);
      }
    }

    public static GH_ValueList CreateDropDown(string name, List<string> values, float x, float y)
    {
      var valueList = new GH_ValueList();
      valueList.CreateAttributes();
      valueList.Name = name;
      valueList.NickName = name + ":";
      valueList.Description = "Select an option...";
      valueList.ListMode = GH_ValueListMode.DropDown;
      valueList.ListItems.Clear();

      for (int i = 0; i < values.Count; i++)
      {
        valueList.ListItems.Add(new GH_ValueListItem(values[i], i.ToString()));
      }

      valueList.Attributes.Pivot = new PointF(x - 200, y - 10);

      return valueList;
    }

    public override bool Read(GH_IReader reader)
    {
      try
      {
        SelectedConstructor = ByteArrayToObject<ConstructorInfo>(reader.GetByteArray("SelectedConstructor"));
      }
      catch { }

      try
      {
        Seed = reader.GetString("seed");
      }
      catch { }
      return base.Read(reader);
    }

    public override bool Write(GH_IWriter writer)
    {
      if (SelectedConstructor != null)
      {
        writer.SetByteArray("SelectedConstructor", ObjectToByteArray(SelectedConstructor));
      }

      writer.SetString("seed", Seed);
      return base.Write(writer);
    }

    private static byte[] ObjectToByteArray(object obj)
    {
      BinaryFormatter bf = new BinaryFormatter();
      using (var ms = new MemoryStream())
      {
        bf.Serialize(ms, obj);
        return ms.ToArray();
      }
    }

    private static T ByteArrayToObject<T>(byte[] arrBytes)
    {
      using (var memStream = new MemoryStream())
      {
        var binForm = new BinaryFormatter();
        memStream.Write(arrBytes, 0, arrBytes.Length);
        memStream.Seek(0, SeekOrigin.Begin);
        var obj = binForm.Deserialize(memStream);
        return (T)obj;
      }
    }

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
      //pManager.AddGenericParameter("Debug", "d", "debug output, please ignore", GH_ParamAccess.list);
      pManager.AddParameter(new SpeckleBaseParam("Speckle Object", "O", "Created speckle object", GH_ParamAccess.item));
    }

    protected override void SolveInstance(IGH_DataAccess DA)
    {
      if (SelectedConstructor is null)
      {
        AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "No schema has been selected.");
        return;
      }


      List<object> cParamsValues = new List<object>();
      var cParams = SelectedConstructor.GetParameters();

      for (int i = 0; i < Params.Input.Count; i++)
      {
        var cParam = cParams[i];
        var param = Params.Input[i];
        if (param.Access == GH_ParamAccess.list)
        {
          var inputValues = new List<object>();
          DA.GetDataList(i, inputValues);
          inputValues = inputValues.Select(x => ExtractRealInputValue(x)).ToList();
          cParamsValues.Add(GetObjectListProp(param, inputValues, cParam.ParameterType));
        }
        else if (param.Access == GH_ParamAccess.item)
        {
          object inputValue = null;
          DA.GetData(i, ref inputValue);
          cParamsValues.Add(GetObjectProp(param, ExtractRealInputValue(inputValue), cParam.ParameterType));
        }
      }

      var outputObject = SelectedConstructor.Invoke(cParamsValues.ToArray());

      ((Base)outputObject).applicationId = $"{Seed}-{SelectedConstructor.Name}-{DA.Iteration}";
      ((Base)outputObject).units = Units.GetUnitsFromString(Rhino.RhinoDoc.ActiveDoc.GetUnitSystemName(true, false, false, false));

      DA.SetData(0, new GH_SpeckleBase() { Value = outputObject as Base });
    }

    private object ExtractRealInputValue(object inputValue)
    {
      if (inputValue == null)
        return null;

      if (inputValue is Grasshopper.Kernel.Types.IGH_Goo)
      {
        return inputValue.GetType().GetProperty("Value").GetValue(inputValue);
      }

      return inputValue;
    }

    //list input
    private object GetObjectListProp(IGH_Param param, List<object> values, Type t)
    {
      if (!values.Any()) return null;

      var list = (IList)Activator.CreateInstance(t);
      var listElementType = list.GetType().GetGenericArguments().Single();
      foreach (var value in values)
      {
        list.Add(ConvertType(listElementType, value, param.Name));
      }

      return list;
    }

    private object GetObjectProp(IGH_Param param, object value, Type t)
    {
      var convertedValue = ConvertType(t, value, param.Name);
      return convertedValue;
    }

    private object ConvertType(Type type, object value, string name)
    {
      if (value == null)
      {
        return null;
      }

      var typeOfValue = value.GetType();
      if (value == null || typeOfValue == type || type.IsAssignableFrom(typeOfValue))
        return value;

      //needs to be before IsSimpleType
      if (type.IsEnum)
      {
        try
        {
          return Enum.Parse(type, value.ToString());
        }
        catch { }
      }

      // int, doubles, etc
      if (Speckle.Core.Models.Utilities.IsSimpleType(value.GetType()))
      {
        return Convert.ChangeType(value, type);
      }

      if (Converter.CanConvertToSpeckle(value))
      {
        var converted = Converter.ConvertToSpeckle(value);
        //in some situations the converted type is not exactly the type needed by the constructor
        //even if an implicit casting is defined, invoking the constructor will fail because the type is boxed
        //to convert the boxed type, it seems the only valid solution is to use Convert.ChangeType
        //currently, this is to support conversion of Polyline to Polycurve in Objects
        if (converted.GetType() != type && !type.IsAssignableFrom(converted.GetType()))
        {
          return Convert.ChangeType(converted, type);
        }
        return converted;
      }
      else
      {
        // Log conversion error?
      }



      //tries an explicit casting, given that the required type is a variable, we need to use reflection
      //not really sure this method is needed
      try
      {
        MethodInfo castIntoMethod = this.GetType().GetMethod("CastObject").MakeGenericMethod(type);
        return castIntoMethod.Invoke(null, new[] { value });
      }
      catch { }

      AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Unable to set " + name + ".");
      throw new Exception($"Could not covert object to {type}");
    }

    //keep public so it can be picked by reflection
    public static T CastObject<T>(object input)
    {
      return (T)input;
    }

    public bool CanInsertParameter(GH_ParameterSide side, int index) => side == GH_ParameterSide.Input;

    public bool CanRemoveParameter(GH_ParameterSide side, int index) => side == GH_ParameterSide.Input;

    public IGH_Param CreateParameter(GH_ParameterSide side, int index)
    {
      var myParam = new GenericAccessParam
      {
        Name = GH_ComponentParamServer.InventUniqueNickname("ABCD", Params.Input),
        MutableNickName = true,
        Optional = true
      };

      myParam.NickName = myParam.Name;
      //myParam.ObjectChanged += (sender, e) => Debouncer.Start();

      return myParam;
    }

    public bool DestroyParameter(GH_ParameterSide side, int index)
    {
      return true;
    }

    public void VariableParameterMaintenance()
    {
    }

    protected override void BeforeSolveInstance()
    {
      Converter.SetContextDocument(Rhino.RhinoDoc.ActiveDoc);
      Tracker.TrackPageview("objects", "create", "variableinput");
      base.BeforeSolveInstance();
    }
  }
}
