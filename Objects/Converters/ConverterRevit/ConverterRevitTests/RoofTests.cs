﻿using System;
using Autodesk.Revit.DB;
using DB = Autodesk.Revit.DB;
using Objects;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using Wall = Objects.Wall;
using Element = Objects.Element;
using xUnitRevitUtils;
using Autodesk.Revit.UI;

namespace ConverterRevitTests
{
  public class RoofFixture : SpeckleConversionFixture
  {
    public override string TestFile => Globals.GetTestModel("Roof.rvt");
    public override string NewFile => Globals.GetTestModel("Roof_ToNative.rvt");
    public override List<BuiltInCategory> Categories => new List<BuiltInCategory> { BuiltInCategory.OST_Roofs };
    public RoofFixture() : base ()
    {
    }
  }

  public class RoofTests : SpeckleConversionTest, IClassFixture<RoofFixture>
  {
    public RoofTests(RoofFixture fixture)
    {
      this.fixture = fixture;
    }

    [Fact]
    [Trait("Roof", "ToSpeckle")]
    public void RoofToSpeckle()
    {
      NativeToSpeckle();
    }

    #region ToNative

    [Fact]
    [Trait("Roof", "ToNative")]
    public void FloorToNative()
    {
      SpeckleToNative<DB.RoofBase>(AssertRoofEqual);
    }

    [Fact]
    [Trait("Roof", "Selection")]
    public void RoofSelectionToNative()
    {
      SelectionToNative<DB.RoofBase>(AssertRoofEqual);
    }

    private void AssertRoofEqual(DB.RoofBase sourceElem, DB.RoofBase destElem)
    {
      Assert.NotNull(destElem);
      Assert.Equal(sourceElem.Name, sourceElem.Name);

      AssertEqualParam(sourceElem, destElem, BuiltInParameter.ROOF_SLOPE);
      AssertEqualParam(sourceElem, destElem, BuiltInParameter.ROOF_BASE_LEVEL_PARAM);
      AssertEqualParam(sourceElem, destElem, BuiltInParameter.ROOF_CONSTRAINT_LEVEL_PARAM);
      AssertEqualParam(sourceElem, destElem, BuiltInParameter.ROOF_UPTO_LEVEL_PARAM);
    }


    #endregion

  }
}