// ==========================================================
// emoji.js — GitHub-compatible emoji shortcode → Unicode map
// Bundled as embedded resource; loaded by MarkdownRenderService.
// Replaces :shortcode: patterns in markdown text before rendering.
// ==========================================================
(function() {
  'use strict';
  const E = {
    // Smileys & People
    smile:'\uD83D\uDE04', laughing:'\uD83D\uDE06', blush:'\uD83D\uDE0A', smiley:'\uD83D\uDE03',
    relaxed:'\uD83D\uDE0C', smirk:'\uD83D\uDE0F', heart_eyes:'\uD83D\uDE0D', kissing_heart:'\uD83D\uDE18',
    wink:'\uD83D\uDE09', stuck_out_tongue_winking_eye:'\uD83D\uDE1C', grin:'\uD83D\uDE01',
    joy:'\uD83D\uDE02', sweat_smile:'\uD83D\uDE05', sob:'\uD83D\uDE2D', cry:'\uD83D\uDE22',
    angry:'\uD83D\uDE20', rage:'\uD83D\uDE21', disappointed:'\uD83D\uDE1E', confused:'\uD83D\uDE15',
    astonished:'\uD83D\uDE32', flushed:'\uD83D\uDE33', fearful:'\uD83D\uDE28', cold_sweat:'\uD83D\uDE30',
    scream:'\uD83D\uDE31', hushed:'\uD83D\uDE2F', sleepy:'\uD83D\uDE2A', tired_face:'\uD83D\uDE2B',
    unamused:'\uD83D\uDE12', expressionless:'\uD83D\uDE11', no_mouth:'\uD83D\uDE36',
    thinking:'\uD83E\uDD14', zipper_mouth_face:'\uD83E\uDD10', nerd_face:'\uD83E\uDD13',
    sunglasses:'\uD83D\uDE0E', monocle_face:'\uD83E\uDDD0', clown_face:'\uD83E\uDD21',
    tada:'\uD83C\uDF89', sparkles:'\u2728', star:'\u2B50', star2:'\uD83C\uDF1F',
    dizzy:'\uD83D\uDCAB', boom:'\uD83D\uDCA5', anger:'\uD83D\uDCA2', zzz:'\uD83D\uDCA4',
    wave:'\uD83D\uDC4B', ok_hand:'\uD83D\uDC4C', thumbsup:'+1', thumbsdown:'-1',
    clap:'\uD83D\uDC4F', raised_hands:'\uD83D\uDE4C', pray:'\uD83D\uDE4F', muscle:'\uD83D\uDCAA',
    point_right:'\uD83D\uDC49', point_left:'\uD83D\uDC48', point_up:'\uD83D\uDC46',
    point_down:'\uD83D\uDC47', writing_hand:'\u270D\uFE0F', selfie:'\uD83E\uDD33',
    heart:'\u2764\uFE0F', broken_heart:'\uD83D\uDC94', two_hearts:'\uD83D\uDC95',
    100:'\uD83D\uDCAF', fire:'\uD83D\uDD25', sunny:'\u2600\uFE0F', snowflake:'\u2744\uFE0F',
    rainbow:'\uD83C\uDF08', cloud:'\u2601\uFE0F', umbrella:'\uD83C\uDF02',
    // Objects & Tech
    computer:'\uD83D\uDCBB', desktop_computer:'\uD83D\uDDA5\uFE0F', keyboard:'\u2328\uFE0F',
    mouse:'\uD83D\uDDB1\uFE0F', floppy_disk:'\uD83D\uDCAC', cd:'\uD83D\uDCBF', dvd:'\uD83D\uDCC0',
    iphone:'\uD83D\uDCF1', calling:'\uD83D\uDCF2', phone:'\u260E\uFE0F', bulb:'\uD83D\uDCA1',
    mag:'\uD83D\uDD0D', mag_right:'\uD83D\uDD0E', pencil:'\u270F\uFE0F', pencil2:'\u270F\uFE0F',
    scissors:'\u2702\uFE0F', memo:'\uD83D\uDCDD', file_folder:'\uD83D\uDCC1',
    open_file_folder:'\uD83D\uDCC2', books:'\uD83D\uDCDA', book:'\uD83D\uDCD6',
    bookmark:'\uD83D\uDD16', label:'\uD83C\uDFF7\uFE0F', camera:'\uD83D\uDCF7',
    movie_camera:'\uD83C\uDFA5', headphones:'\uD83C\uDFA7', microphone:'\uD83C\uDFA4',
    speaker:'\uD83D\uDD0A', mute:'\uD83D\uDD07', bell:'\uD83D\uDD14', no_bell:'\uD83D\uDD15',
    clock1:'\uD83D\uDD50', alarm_clock:'\u23F0', stopwatch:'\u23F1\uFE0F',
    calendar:'\uD83D\uDCC5', date:'\uD83D\uDCC5', card_index:'\uD83D\uDCC7',
    chart_with_upwards_trend:'\uD83D\uDCC8', chart_with_downwards_trend:'\uD83D\uDCC9',
    bar_chart:'\uD83D\uDCCA', clipboard:'\uD83D\uDCCB', pushpin:'\uD83D\uDCCC',
    paperclip:'\uD83D\uDCCE', link:'\uD83D\uDD17', lock:'\uD83D\uDD12', unlock:'\uD83D\uDD13',
    key:'\uD83D\uDD11', hammer:'\uD83D\uDD28', wrench:'\uD83D\uDD27', gear:'\u2699\uFE0F',
    shield:'\uD83D\uDEE1\uFE0F', warning:'\u26A0\uFE0F', zap:'\u26A1',
    recycle:'\u267B\uFE0F', white_check_mark:'\u2705', x:'\u274C',
    heavy_check_mark:'\u2714\uFE0F', heavy_plus_sign:'\u2795', heavy_minus_sign:'\u2796',
    question:'\u2753', exclamation:'\u2757', grey_question:'\u2754',
    information_source:'\u2139\uFE0F', new:'\uD83C\uDD95', up:'\uD83C\uDD99',
    cool:'\uD83C\uDD92', free:'\uD83C\uDD93', ng:'\uD83C\uDD96', ok:'\uD83C\uDD97',
    // Animals & Nature
    bug:'\uD83D\uDC1B', ant:'\uD83D\uDC1C', bee:'\uD83D\uDC1D', butterfly:'\uD83E\uDD8B',
    snake:'\uD83D\uDC0D', dragon:'\uD83D\uDC09', turtle:'\uD83D\uDC22', whale:'\uD83D\uDC33',
    elephant:'\uD83D\uDC18', lion:'\uD83E\uDD81', cat:'\uD83D\uDC31', dog:'\uD83D\uDC36',
    mouse2:'\uD83D\uDC2D', rabbit:'\uD83D\uDC30', bear:'\uD83D\uDC3B',
    panda_face:'\uD83D\uDC3C', penguin:'\uD83D\uDC27', bird:'\uD83D\uDC26',
    seedling:'\uD83C\uDF31', evergreen_tree:'\uD83C\uDF32', deciduous_tree:'\uD83C\uDF33',
    palm_tree:'\uD83C\uDF34', cactus:'\uD83C\uDF35', tulip:'\uD83C\uDF37',
    rose:'\uD83C\uDF39', sunflower:'\uD83C\uDF3B', four_leaf_clover:'\uD83C\uDF40',
    mushroom:'\uD83C\uDF44', earth_africa:'\uD83C\uDF0D', earth_americas:'\uD83C\uDF0E',
    earth_asia:'\uD83C\uDF0F', globe_with_meridians:'\uD83C\uDF10',
    // Food & Travel
    coffee:'\u2615', tea:'\uD83C\uDF75', pizza:'\uD83C\uDF55', hamburger:'\uD83C\uDF54',
    sushi:'\uD83C\uDF63', cake:'\uD83C\uDF82', beer:'\uD83C\uDF7A', wine_glass:'\uD83C\uDF77',
    tropical_drink:'\uD83C\uDF79', rocket:'\uD83D\uDE80', airplane:'\u2708\uFE0F',
    car:'\uD83D\uDE97', bus:'\uD83D\uDE8C', bicycle:'\uD83D\uDEB2', anchor:'\u2693',
    // Flags & symbols
    checkered_flag:'\uD83C\uDFC1', triangular_flag_on_post:'\uD83D\uDEA9',
    crossed_flags:'\uD83C\uCDF4', white_flag:'\uD83C\uDFF3\uFE0F',
    black_flag:'\uD83C\uDFF4', fr:'\uD83C\uDDEB\uD83C\uDDF7', us:'\uD83C\uDDFA\uD83C\uDDF8',
    gb:'\uD83C\uDDEC\uD83C\uDDE7', de:'\uD83C\uDDE9\uD83C\uDDEA', jp:'\uD83C\uDDEF\uD83C\uDDF5',
  };

  // Register marked extension to replace :shortcode: with emoji
  if (typeof marked !== 'undefined' && marked.use) {
    marked.use({
      extensions: [{
        name: 'emoji',
        level: 'inline',
        start(src) { return src.indexOf(':'); },
        tokenizer(src) {
          const m = src.match(/^:([a-zA-Z0-9_+\-]+):/);
          if (m) return { type: 'emoji', raw: m[0], code: m[1] };
        },
        renderer(token) {
          const ch = E[token.code];
          return ch ? `<span class="emoji" title=":${token.code}:">${ch}</span>` : token.raw;
        }
      }]
    });
  }
})();
