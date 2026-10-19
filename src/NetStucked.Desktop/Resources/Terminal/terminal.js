'use strict';
(() => {
  const sessions = new Map(); let selected = '', dark = false;
  const send = value => chrome.webview.postMessage(value);
  const theme = () => dark ? { background:'#182230',foreground:'#E4ECF7',cursor:'#E4ECF7',selectionBackground:'#41546E',black:'#182230',red:'#FF8590',green:'#56DBA4',yellow:'#FFC16C',blue:'#75AAFF',magenta:'#DEA8FF',cyan:'#72DDEB',white:'#E4ECF7',brightBlack:'#A0B1C8',brightRed:'#FFA5AD',brightGreen:'#8FE7BD',brightYellow:'#FFD69E',brightBlue:'#A7C9FF',brightMagenta:'#EBC8FF',brightCyan:'#AEEDF4',brightWhite:'#FFFFFF'} : { background:'#FFFFFF',foreground:'#172B52',cursor:'#172B52',selectionBackground:'#C7D7EC',black:'#172B52',red:'#B42332',green:'#006B44',yellow:'#805000',blue:'#1D56C9',magenta:'#833AB4',cyan:'#006777',white:'#59687E',brightBlack:'#566783',brightRed:'#B42332',brightGreen:'#006B44',brightYellow:'#805000',brightBlue:'#1D56C9',brightMagenta:'#833AB4',brightCyan:'#006777',brightWhite:'#172B52'};
  function fit(s) { if (s.root.classList.contains('active')) { s.fit.fit(); if(s.term.cols > 400 || s.term.rows > 200) s.term.resize(Math.min(400,s.term.cols),Math.min(200,s.term.rows)); } }
  function select(id) { selected=id; for(const [key,s] of sessions) s.root.classList.toggle('active',key===id); const s=sessions.get(id); if(s) {fit(s);s.term.focus();} }
  function create(id) {
    if(sessions.size>=5) throw Error('Tab limit');
    const root=document.createElement('div');root.className='session'; document.getElementById('sessions').append(root);
    const term=new Terminal({cols:100,rows:30,scrollback:5000,fontFamily:'Consolas, monospace',fontSize:14,theme:theme(),cursorBlink:true,allowProposedApi:false,allowTransparency:false,convertEol:false,linkHandler:{activate:()=>{}}});
    const fitAddon=new FitAddon.FitAddon();term.loadAddon(fitAddon);term.open(root);
    const s={term,fit:fitAddon,root,generation:'',fontSize:14};sessions.set(id,s);
    // Retain the emulator on its owned view node for accessibility/visual QA inspection.
    root.terminal=term;
    const input=bytes=>{for(let p=0;p<bytes.length;p+=4096)send({kind:'input',tab:id,generation:s.generation,data:btoa(String.fromCharCode(...bytes.subarray(p,p+4096)))});};
    term.onData(data=>input(new TextEncoder().encode(data)));
    term.onBinary(data=>input(Uint8Array.from(data,c=>c.charCodeAt(0))));
    term.onResize(({cols,rows})=>send({kind:'resize',tab:id,generation:s.generation,cols,rows}));
    // A remote OSC 52 sequence must never write the operator's clipboard.
    term.parser.registerOscHandler(52,()=>true);
    term.attachCustomKeyEventHandler(e=>{
      if(e.type!=='keydown')return true;
      if((e.ctrlKey&&e.shiftKey&&e.code==='KeyC') || (e.ctrlKey&&!e.shiftKey&&e.code==='KeyC'&&term.hasSelection())) {send({kind:'copy',tab:id});return false;}
      if((e.ctrlKey&&e.code==='KeyV') || (e.shiftKey&&e.code==='Insert')) {e.preventDefault();send({kind:'paste',tab:id});return false;}
      if(e.ctrlKey&&e.code==='Digit0'){s.fontSize=14;term.options.fontSize=14;fit(s);return false;}
      return true;
    });
    root.addEventListener('paste',e=>{e.preventDefault();e.stopImmediatePropagation();send({kind:'paste',tab:id});},true);
    root.addEventListener('contextmenu',e=>e.preventDefault());
    root.addEventListener('wheel',e=>{if(e.ctrlKey){e.preventDefault();e.stopPropagation();s.fontSize=Math.max(8,Math.min(28,s.fontSize+(e.deltaY<0?1:-1)));term.options.fontSize=s.fontSize;fit(s);}}, {capture:true,passive:false});
    return s;
  }
  chrome.webview.addEventListener('message',async event=>{
    const m=event.data;
    try {
      let s=sessions.get(m.tab);
      switch(m.kind) {
        case 'begin': s=s||create(m.tab);s.generation=m.generation;termReset(s);if(s.root.classList.contains('active'))send({kind:'resize',tab:m.tab,generation:s.generation,cols:s.term.cols,rows:s.term.rows});break;
        case 'select': select(m.tab);break;
        case 'remove': if(s){s.term.dispose();s.root.remove();sessions.delete(m.tab);}break;
        case 'write': if(!s||s.generation!==m.generation)throw Error('Stale terminal');const bytes=Uint8Array.from(atob(m.data),c=>c.charCodeAt(0));if(bytes.length>4096)throw Error('Frame too large');await new Promise(resolve=>s.term.write(bytes,resolve));break;
        case 'paste': if(s&&s.generation===m.generation)s.term.paste(m.text);break;
        case 'copy': send({kind:'selection',tab:m.tab,text:s?s.term.getSelection().slice(0,65536):''});break;
        case 'theme': dark=m.dark;document.body.style.background=theme().background;for(const v of sessions.values())v.term.options.theme=theme();break;
        default: throw Error('Unknown command');
      }
      if(m.seq)send({kind:'ack',seq:m.seq});
    } catch { if(m.seq)send({kind:'error',seq:m.seq}); }
  });
  function termReset(s){s.term.reset();s.term.clear();}
  new ResizeObserver(()=>{const s=sessions.get(selected);if(s)fit(s);}).observe(document.body);
  send({kind:'ready'});
})();
