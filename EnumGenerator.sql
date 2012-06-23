SELECT
    mEnum.EnumType
    ,mEnumType.Summary AS EnumTypeSummary
    ,0 AS EnumTypeFlags
    ,mEnum.EnumName
    ,mEnum.EnumValue
    ,mEnum.Summary AS EnumSummary
FROM
    mEnum
INNER JOIN mEnumType
    ON mEnumType.EnumType = mEnum.EnumType