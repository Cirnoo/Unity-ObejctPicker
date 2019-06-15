# ObejctPicker 适用于Unity大型项目的对象选择器

### 简介
unity的ObjectField使用的对象选择器在Asset数量很多的时候是很慢的，特别是在大型项目中 ，每次搜索都会消耗大量的时间。
为了解决搜索慢的问题，尝试采用Unity的全局搜索来替换掉ObjectField的搜索  
关于ObjectField请见->[https://docs.unity3d.com/ScriptReference/EditorGUI.ObjectField.html]

### 原理
ObjectField的搜索方法是一次性在全局搜索某种类型的资源，然后再通过遍历来实现按名称搜索功能，这就导致资源数量多的时候搜索很慢。所以考虑采用Unity的AssetDatabase.FindAssets函数来加速。   
但是FindAssets只返回了资源的Guid，而当子资源存在的时候，子资源和父资源会使用同一个Guid，这就导致无法正确的搜索到子资源，见代码的ObejctPicker实现。  
为了解决这个问题查了下Unity2017的源码，获取到了Asset的InstanceID，但是这个接口是不开放的，而且新版本的Unity接口名称有所变化，所以通用性不强。见代码的ObejctPickerBeta实现。  


