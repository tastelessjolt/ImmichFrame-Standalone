<?xml version="1.0" encoding="utf-8"?>
<xsl:stylesheet version="1.0"
  xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
  xmlns:android="http://schemas.android.com/apk/res/android">

  <xsl:output method="xml" encoding="utf-8" indent="yes" />

  <xsl:template match="@*|node()">
    <xsl:copy>
      <xsl:apply-templates select="@*|node()" />
    </xsl:copy>
  </xsl:template>

  <xsl:template match="provider[@android:process=':screen_schedule' and starts-with(@android:name, 'mono.MonoRuntimeProvider')]" />
</xsl:stylesheet>
