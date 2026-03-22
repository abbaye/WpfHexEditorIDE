// ==========================================================
// classdiagram.js — VS-Like Class Diagram Static Preview Renderer
// Used by MarkdownRenderService to render classDiagram fenced blocks
// as inline SVG in the Markdown WebView2 preview pane.
//
// Entry point: ClassDiagramPreview.renderAll(theme)
//   theme: 'dark' | 'light'
//   Scans document for .cd-preview[data-cd] elements,
//   decodes the base64 DSL text, parses + renders as SVG.
// ==========================================================

var ClassDiagramPreview = (function () {
  'use strict';

  // --- Layout constants --------------------------------------------------
  var BOX_MIN_WIDTH   = 160;
  var BOX_PADDING_H   = 14;
  var BOX_PADDING_V   = 6;
  var HEADER_HEIGHT   = 38;
  var MEMBER_HEIGHT   = 18;
  var SECTION_GAP     = 4;
  var COL_SPACING     = 60;
  var ROW_SPACING     = 80;
  var CANVAS_PADDING  = 40;
  var ICON_SIZE       = 9;

  // --- DSL Parser --------------------------------------------------------

  function parseDsl(text) {
    var lines   = text.split(/\r?\n/);
    var classes = [];
    var rels    = [];
    var current = null;

    for (var i = 0; i < lines.length; i++) {
      var raw  = lines[i];
      var line = raw.trim();
      if (!line || line.startsWith('#') || line.startsWith('//')) continue;

      // Class/interface/enum/struct/abstract declaration
      var classMatch = line.match(/^(abstract\s+class|class|interface|enum|struct)\s+(\w+)(?:\s+extends\s+(\w+))?\s*\{?/i);
      if (classMatch) {
        var kind = classMatch[1].replace(/\s+/g, ' ').toLowerCase();
        var name = classMatch[2];
        var ext  = classMatch[3] || null;
        current  = { name: name, kind: kind, members: [] };
        classes.push(current);
        if (ext) rels.push({ src: name, tgt: ext, kind: 'inheritance', label: null });
        continue;
      }

      // One-liner "abstract class Foo extends Bar" (no brace)
      var oneLiner = line.match(/^(abstract\s+class|class|interface|enum|struct)\s+(\w+)\s+extends\s+(\w+)\s*$/i);
      if (oneLiner) {
        var name2 = oneLiner[2];
        var ext2  = oneLiner[3];
        if (!classes.find(function(c){ return c.name === name2; }))
          classes.push({ name: name2, kind: oneLiner[1].toLowerCase().replace(/\s+/g,' '), members: [] });
        rels.push({ src: name2, tgt: ext2, kind: 'inheritance', label: null });
        current = null;
        continue;
      }

      // Closing brace
      if (line === '}') { current = null; continue; }

      // Relationship lines (outside class blocks)
      if (!current) {
        var relMatch = line.match(/^(\w+)\s*(-->|\.\.>|o--|<\|--|<--|<\.\.)\s*(\w+)(?:\s*:\s*(.+))?$/);
        if (relMatch) {
          var rk = relMatch[2] === '-->'   ? 'association'
                 : relMatch[2] === '..>'   ? 'dependency'
                 : relMatch[2] === 'o--'   ? 'aggregation'
                 : relMatch[2] === '<|--'  ? 'inheritance'
                 : 'association';
          rels.push({ src: relMatch[1], tgt: relMatch[3], kind: rk, label: relMatch[4] || null });
        }
        continue;
      }

      // Member lines inside a class block
      var member = parseMember(line);
      if (member) current.members.push(member);
    }

    return { classes: classes, rels: rels };
  }

  function parseMember(line) {
    // field keyword or - prefix
    if (/^(field\s+|-)\s*(\w+)/.test(line)) {
      var m = line.match(/^(?:field\s+|-\s*)(\w+)(?:\s*:\s*(.+))?/);
      return m ? { kind: 'field', name: m[1], typeName: m[2] || '' } : null;
    }
    // property keyword or + prefix
    if (/^(property\s+|\+)\s*(\w+)/.test(line)) {
      var m = line.match(/^(?:property\s+|\+\s*)(\w+)(?:\s*:\s*(.+))?/);
      return m ? { kind: 'property', name: m[1], typeName: m[2] || '' } : null;
    }
    // event keyword
    if (/^event\s+/.test(line)) {
      var m = line.match(/^event\s+(\w+)/);
      return m ? { kind: 'event', name: m[1], typeName: '' } : null;
    }
    // method keyword or trailing ()
    if (/^method\s+/.test(line) || /\(/.test(line)) {
      var m = line.match(/^(?:method\s+)?(\w+\s*\(.*?\))(?:\s*:\s*(.+))?/);
      return m ? { kind: 'method', name: m[1], typeName: m[2] || '' } : null;
    }
    return null;
  }

  // --- Layout Engine -----------------------------------------------------

  function layoutDocument(doc) {
    // Build inheritance forest (tree layers)
    var childMap  = {};  // parent → [children]
    var hasParent = {};
    doc.rels.forEach(function(r) {
      if (r.kind === 'inheritance') {
        if (!childMap[r.tgt]) childMap[r.tgt] = [];
        childMap[r.tgt].push(r.src);
        hasParent[r.src] = true;
      }
    });

    // BFS from roots
    var roots  = doc.classes.filter(function(c) { return !hasParent[c.name]; });
    var placed = {};
    var layers = [];

    function bfs(nodes, depth) {
      if (!nodes.length) return;
      layers[depth] = layers[depth] || [];
      var next = [];
      nodes.forEach(function(n) {
        if (placed[n.name]) return;
        placed[n.name] = true;
        layers[depth].push(n);
        (childMap[n.name] || []).forEach(function(cn) {
          var child = doc.classes.find(function(c) { return c.name === cn; });
          if (child && !placed[child.name]) next.push(child);
        });
      });
      bfs(next, depth + 1);
    }
    bfs(roots, 0);

    // Place any unplaced classes in extra rows
    doc.classes.forEach(function(c) {
      if (!placed[c.name]) {
        layers.push([c]);
        placed[c.name] = true;
      }
    });

    // Compute box sizes
    var sizes = {};
    doc.classes.forEach(function(c) { sizes[c.name] = computeBoxSize(c); });

    // Assign positions
    var positions = {};
    var y = CANVAS_PADDING;
    layers.forEach(function(layer) {
      var rowH = 0;
      var x    = CANVAS_PADDING;
      layer.forEach(function(c) {
        var sz = sizes[c.name];
        positions[c.name] = { x: x, y: y, w: sz.w, h: sz.h };
        x  += sz.w + COL_SPACING;
        rowH = Math.max(rowH, sz.h);
      });
      y += rowH + ROW_SPACING;
    });

    return { positions: positions, sizes: sizes,
             canvasW: computeCanvasW(positions, sizes),
             canvasH: y };
  }

  function computeBoxSize(cls) {
    var membersByKind = groupMembers(cls.members);
    var longestName   = cls.name.length;
    cls.members.forEach(function(m) {
      var len = (m.name + (m.typeName ? ' : ' + m.typeName : '')).length + 3;
      if (len > longestName) longestName = len;
    });
    var w = Math.max(BOX_MIN_WIDTH, longestName * 7 + BOX_PADDING_H * 2);
    var sectionCount = Object.keys(membersByKind).filter(function(k) { return membersByKind[k].length; }).length;
    var h = HEADER_HEIGHT
          + sectionCount * (SECTION_GAP + 2)
          + cls.members.length * MEMBER_HEIGHT
          + BOX_PADDING_V * 2;
    return { w: w, h: Math.max(h, 60) };
  }

  function computeCanvasW(positions, sizes) {
    var maxX = 0;
    Object.keys(positions).forEach(function(name) {
      var p = positions[name]; var s = sizes[name];
      var r = p.x + s.w + CANVAS_PADDING;
      if (r > maxX) maxX = r;
    });
    return maxX;
  }

  function groupMembers(members) {
    var g = { field: [], property: [], method: [], event: [] };
    members.forEach(function(m) { if (g[m.kind]) g[m.kind].push(m); });
    return g;
  }

  // --- SVG Renderer ------------------------------------------------------

  function renderSvg(doc, layout, theme) {
    var isDark = theme === 'dark';
    var defs   = buildDefs(isDark);
    var boxes  = '';
    var arrows = '';

    // Render relationship arrows first (behind boxes)
    doc.rels.forEach(function(r) {
      var sp = layout.positions[r.src];
      var tp = layout.positions[r.tgt];
      if (!sp || !tp) return;
      var ss = layout.sizes[r.src];
      var ts = layout.sizes[r.tgt];
      arrows += renderArrow(r, sp, ss, tp, ts, isDark);
    });

    // Render class boxes
    doc.classes.forEach(function(cls) {
      var pos = layout.positions[cls.name];
      var sz  = layout.sizes[cls.name];
      if (!pos) return;
      boxes += renderBox(cls, pos, sz, isDark);
    });

    var w = layout.canvasW;
    var h = layout.canvasH;
    return '<svg xmlns="http://www.w3.org/2000/svg" width="' + w + '" height="' + h + '" viewBox="0 0 ' + w + ' ' + h + '">'
         + defs
         + '<rect width="' + w + '" height="' + h + '" class="cd-canvas-bg"/>'
         + arrows
         + boxes
         + '</svg>';
  }

  function buildDefs(isDark) {
    var arrowColor     = isDark ? '#569CD6' : '#2E75B6';
    var inheritColor   = isDark ? '#4FC1FF' : '#2E75B6';
    return '<defs>'
         + '<marker id="cd-arr-open" markerWidth="8" markerHeight="8" refX="6" refY="3" orient="auto">'
         + '<path d="M0,0 L6,3 L0,6" stroke="' + arrowColor + '" stroke-width="1" fill="none"/></marker>'
         + '<marker id="cd-arr-inherit" markerWidth="12" markerHeight="10" refX="10" refY="5" orient="auto">'
         + '<path d="M0,0 L10,5 L0,10 Z" stroke="' + inheritColor + '" stroke-width="1" fill="' + (isDark ? '#2A2A2A' : '#F5F5F5') + '"/></marker>'
         + '<marker id="cd-arr-diamond" markerWidth="10" markerHeight="8" refX="1" refY="4" orient="auto">'
         + '<path d="M0,4 L5,0 L10,4 L5,8 Z" stroke="' + arrowColor + '" stroke-width="1" fill="none"/></marker>'
         + '</defs>';
  }

  function renderArrow(rel, sp, ss, tp, ts, isDark) {
    // Connect bottom-center of source to top-center of target
    var x1 = sp.x + ss.w / 2;
    var y1 = sp.y + ss.h;
    var x2 = tp.x + ts.w / 2;
    var y2 = tp.y;

    // If target is above source, flip
    if (tp.y + ts.h < sp.y) { y1 = sp.y; y2 = tp.y + ts.h; }

    var markerEnd = rel.kind === 'inheritance' ? 'url(#cd-arr-inherit)'
                  : rel.kind === 'aggregation' ? 'url(#cd-arr-diamond)'
                  : 'url(#cd-arr-open)';

    var isDep   = rel.kind === 'dependency';
    var cls     = isDep ? 'cd-dep-arrow' : (rel.kind === 'inheritance' ? 'cd-inherit-arrow' : 'cd-arrow');
    var dash    = isDep ? ' stroke-dasharray="5,3"' : '';

    var mid1y   = y1 + (y2 - y1) / 2;
    var path    = 'M ' + x1 + ' ' + y1 + ' C ' + x1 + ' ' + mid1y + ' ' + x2 + ' ' + mid1y + ' ' + x2 + ' ' + y2;
    var svg     = '<path d="' + path + '" class="' + cls + '"' + dash + ' marker-end="' + markerEnd + '"/>';

    if (rel.label) {
      var lx = (x1 + x2) / 2 + 4;
      var ly = (y1 + y2) / 2 - 4;
      svg += '<text x="' + lx + '" y="' + ly + '" class="cd-arrow-label">' + escapeXml(rel.label) + '</text>';
    }
    return svg;
  }

  function renderBox(cls, pos, sz, isDark) {
    var x = pos.x, y = pos.y, w = sz.w;
    var svg = '<g transform="translate(' + x + ',' + y + ')">';

    // Box background + border
    svg += '<rect width="' + w + '" height="' + sz.h + '" rx="3" class="cd-box-bg cd-box-border" stroke-width="1"/>';

    // Header background
    svg += '<rect width="' + w + '" height="' + HEADER_HEIGHT + '" rx="3" class="cd-header-bg"/>';
    // Clip bottom corners of header rect
    svg += '<rect y="' + (HEADER_HEIGHT / 2) + '" width="' + w + '" height="' + (HEADER_HEIGHT / 2) + '" class="cd-header-bg"/>';

    // Stereotype label
    var stereo = kindToStereotype(cls.kind);
    var headerCy = 12;
    if (stereo) {
      svg += '<text x="' + (w / 2) + '" y="' + headerCy + '" text-anchor="middle" class="cd-stereo-text">' + stereo + '</text>';
      headerCy += 13;
    }
    // Class name
    svg += '<text x="' + (w / 2) + '" y="' + headerCy + '" text-anchor="middle" class="cd-header-text" font-size="13">' + escapeXml(cls.name) + '</text>';

    // Members by section
    var groups = groupMembers(cls.members);
    var cy     = HEADER_HEIGHT + SECTION_GAP;
    var order  = ['field', 'property', 'method', 'event'];

    order.forEach(function(kind) {
      var members = groups[kind];
      if (!members || !members.length) return;

      // Section divider
      svg += '<line x1="0" y1="' + cy + '" x2="' + w + '" y2="' + cy + '" class="cd-divider"/>';
      cy += SECTION_GAP;

      members.forEach(function(m) {
        // Icon
        var iconX = BOX_PADDING_H;
        var iconY = cy + MEMBER_HEIGHT / 2 - ICON_SIZE / 2;
        svg += renderMemberIcon(kind, iconX, iconY);

        // Member text
        var label = m.name + (m.typeName ? ' : ' + m.typeName : '');
        var textX = BOX_PADDING_H + ICON_SIZE + 4;
        var textY = cy + MEMBER_HEIGHT - 4;
        svg += '<text x="' + textX + '" y="' + textY + '" class="cd-member-text">' + escapeXml(label) + '</text>';
        cy += MEMBER_HEIGHT;
      });
    });

    svg += '</g>';
    return svg;
  }

  function renderMemberIcon(kind, x, y) {
    var s = ICON_SIZE;
    if (kind === 'field') {
      return '<circle cx="' + (x + s/2) + '" cy="' + (y + s/2) + '" r="' + (s/2) + '" class="cd-field-icon"/>';
    }
    if (kind === 'property') {
      return '<rect x="' + x + '" y="' + y + '" width="' + s + '" height="' + s + '" class="cd-prop-icon"/>';
    }
    if (kind === 'method') {
      var hx = x + s/2, hy = y + s/2;
      return '<polygon points="' + hx + ',' + y + ' ' + (x+s) + ',' + hy + ' ' + hx + ',' + (y+s) + ' ' + x + ',' + hy + '" class="cd-method-icon"/>';
    }
    if (kind === 'event') {
      return '<polygon points="' + (x+4) + ',' + y + ' ' + (x+s) + ',' + (y+4) + ' ' + (x+5) + ',' + (y+4) + ' ' + (x+s-4) + ',' + (y+s) + ' ' + x + ',' + (y+5) + ' ' + (x+4) + ',' + (y+5) + '" class="cd-event-icon"/>';
    }
    return '';
  }

  function kindToStereotype(kind) {
    if (kind === 'interface')       return '«interface»';
    if (kind === 'enum')            return '«enum»';
    if (kind === 'struct')          return '«struct»';
    if (kind === 'abstract class')  return '«abstract»';
    return '';
  }

  function escapeXml(s) {
    return String(s)
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;');
  }

  // --- Public API --------------------------------------------------------

  function renderAll(theme) {
    var nodes = document.querySelectorAll('.cd-preview[data-cd]');
    for (var i = 0; i < nodes.length; i++) {
      var node = nodes[i];
      try {
        var encoded = node.getAttribute('data-cd');
        var dsl     = decodeURIComponent(escape(atob(encoded)));
        var doc     = parseDsl(dsl);
        var layout  = layoutDocument(doc);
        var svg     = renderSvg(doc, layout, theme);
        node.setAttribute('data-cd-theme', theme);
        node.innerHTML = svg;
      } catch(e) {
        node.innerHTML = '<div style="color:red;font-size:11px">classDiagram render error: ' + e.message + '</div>';
      }
    }
  }

  return { renderAll: renderAll, parseDsl: parseDsl };
})();
