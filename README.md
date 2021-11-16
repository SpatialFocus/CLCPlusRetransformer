# ClcPlusRetransformer

```bash
git tag -a v1.1 -m "Release 1.1 with xyz"
git push origin <tag_name>
```

## Processing workflow

The following steps outline the processing of the Retransformer tool in the non-tiled (PartitionCount = 1) setting:

- __Load__ the 3 input datasets from their shape file or geopackage source
- __Clip__ the datasets using the aoi coordinates or aoi (multi-)polygon input (Optional)
- __Dissolve__ both the _hardbone rasterized vector lines_ as well as the _backbone lines_ (after converting them __polygons to lines__)
- Calculate the __difference__ between the two line datasets, calculate their __union__ and __dissolve__
- __Simplify__ and __Smooth__ the above difference and __snap to__ the _baseline hardbone smooth vector lines_, then __merge__ these two and __node__ the result
- __Merge__ the lines with the outline of the dataset (AOI or dataset extent) to have outer boundaries for the final polygonize step
- __Node__ the lines on a precision model grid
- __Union__ the merged lines and __polygonize__ them
- __Clip__ the polygons with the EEA Border outline (Optional)
- __Eliminate polygons__ by merging them to larger polygons but not across a _hardbone_
- __Eliminate by merging__ small polygons together while still obeying the _hardbone_ boundaries
- __Force eliminate polygons__ to remove the few still existing small polygons and merge them over the _hardbone_

## Processing tools implementation

Here are more implementation details and configuration settings of selected processing tools:

- __Eliminate__ removes polygons below a threshold of 5000m2 using 3 different iterative approaches
- __Node__ iterates over all vertices and aligns them with given precision model grid using Snap-Rounding (default value: 0.01mm)
- __Simplify__ uses the [NetTopologySuite](https://github.com/NetTopologySuite/NetTopologySuite) implementation of the [DouglasPeuckerSimplifier](https://github.com/NetTopologySuite/NetTopologySuite/blob/master/src/NetTopologySuite/Simplify/DouglasPeuckerSimplifier.cs) with a 15m threshold
- __Smooth__ uses Chaikin's corner cutting algorithm and sets new line vertices at 15% and 85% of the distance on each curve
- __Snap to__ iterates over each curve (line) and for both start- and endpoint looks for the closest vertex in the target layer. If the closest vertex is within the threshold distance it snaps to the vertex, otherwise we fallback to the nearest point on the target lines and check if this is within the threshold distance. The default threshold is 17m.

----

Made with :heart: by [Spatial Focus](https://spatial-focus.net/)
