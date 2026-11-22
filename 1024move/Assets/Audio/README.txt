真实音频替换说明
=================

把 .wav / .ogg / .mp3 文件放进这个文件夹，文件名必须等于音效 id。
编辑器会自动把文件镜像到 Assets/Resources/Audio/，运行时优先播放真实音频；
镜像失败或文件不存在时，自动退回代码合成的占位音。

音效 id 一览：

  sfx_click       UI 点击
  sfx_tick        磁带倒带刻度
  sfx_type        打字机"哒"
  sfx_pickup      拾取（杂物 / 抹布 / 旧物）
  sfx_store       丢进纸箱
  sfx_box_drop    搬家纸箱落地（沉闷碰撞）
  sfx_tape_rip    撕封箱胶带
  sfx_dust        灰尘消散
  sfx_wipe        擦桌子沙沙（循环）
  sfx_tv_click    老电视拍打"哐哐"
  sfx_tv_snow     电视雪花底噪（循环）
  sfx_metal       铁盒金属碰撞
  sfx_tinnitus    记忆穿梭耳鸣
  hum_ambient     房间底噪（循环）
  hum_tape        磁带哼唱
  bgm_room        背景音乐（怀旧千禧年，循环）

示例：
  Assets/Audio/sfx_box_drop.wav
  Assets/Audio/sfx_wipe.wav
