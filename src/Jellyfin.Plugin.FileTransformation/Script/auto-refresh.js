<script>
(function(){
    var ftVer=null,ftMode='__FT_MODE__',ftDebug=__FT_DEBUG__;
    var ftShown=false,ftPending=false,ftActScheduled=false;
    var FT_MAX_RELOADS=3,FT_RELOAD_WINDOW=60000;
    function ftLog(){if(ftDebug)console.log.apply(console,['[FT]'].concat(Array.prototype.slice.call(arguments)));}
    function ftDbg(){if(ftDebug)console.debug.apply(console,['[FT]'].concat(Array.prototype.slice.call(arguments)));}
    function ftIsHome(){
        var h=window.location.hash||'';
        return h===''||h==='#/'||h==='#/home.html'||h.indexOf('#/home')===0;
    }
    function ftCanReload(){
        var now=Date.now();
        var s=sessionStorage;
        var count=parseInt(s.getItem('ft_rl_c')||'0',10);
        var start=parseInt(s.getItem('ft_rl_s')||'0',10);
        if(now-start>FT_RELOAD_WINDOW){
            count=0;
            start=now;
        }
        count++;
        s.setItem('ft_rl_c',count);
        s.setItem('ft_rl_s',start);
        if(count>FT_MAX_RELOADS){
            ftLog('reload loop detected ('+count+' reloads in '+FT_RELOAD_WINDOW/1000+'s), stopping');
            return false;
        }
        return true;
    }
    function ftAct(){
        ftActScheduled=false;
        if(ftMode==='reload'){
            if(ftIsHome()){
                if(ftCanReload()){
                    ftPending=false;
                    ftLog('on home, reloading now');
                    window.location.reload();
                }else{
                    ftPending=false;
                    if(!ftShown){
                        ftShown=true;
                        ftShowToast();
                    }
                }
            }else if(!ftShown){
                ftShown=true;
                ftShowReloadToast();
            }
        }else if(ftMode==='toast'){
            ftPending=false;
            if(!ftShown){
                ftShown=true;
                ftShowToast();
            }
        }
    }
    function ftGetBase(){
        var p=window.location.pathname;
        var i=p.indexOf('/web');
        return i>0?p.substring(0,i):'';
    }
    function ftCheck(){
        var x=new XMLHttpRequest();
        x.open('GET',ftGetBase()+'/FileTransformation/config-version',true);
        x.timeout=4000;
        x.ontimeout=function(){ftDbg('poll timeout');};
        x.onerror=function(){ftDbg('poll network error');};
        x.onload=function(){
            if(x.status===200){
                try{
                    var v=JSON.parse(x.responseText).version;
                    if(ftVer===null){ftVer=v;ftDbg('init version:',v);return;}
                    if(v!==ftVer){
                        ftLog('version changed:',ftVer,'->',v);
                        ftVer=v;
                        ftPending=true;
                        ftShown=false;
                    }
                    if(ftPending&&!ftActScheduled){
                        ftDbg('pending=true, isHome='+ftIsHome()+', hash='+window.location.hash);
                        ftActScheduled=true;
                        ftDbg('acting in 3s (waiting for plugins to process config)');
                        setTimeout(ftAct,3000);
                    }
                }catch(e){ftLog('poll error:',e);}
            }
        };
        x.send();
    }
    function ftDismiss(el){el.remove();ftShown=false;}
    function ftShowReloadToast(){
        if(document.getElementById('ft-config-toast'))return;
        var d=document.createElement('div');
        d.id='ft-config-toast';
        d.style.cssText='position:fixed;bottom:1.5em;left:50%;transform:translateX(-50%);z-index:10000;background:#1e1e1e;color:#eee;border:1px solid rgba(255,255,255,0.15);border-radius:8px;padding:0.8em 1.2em;display:flex;align-items:center;gap:1em;font-family:inherit;font-size:0.95em;box-shadow:0 4px 20px rgba(0,0,0,0.5);';
        var txt=document.createElement('span');
        txt.textContent='Settings changed. Will reload on home page.';
        d.appendChild(txt);
        var btn=document.createElement('button');
        btn.textContent='Reload Now';
        btn.style.cssText='background:#00a4dc;color:#fff;border:none;border-radius:4px;padding:0.4em 1em;cursor:pointer;font-size:0.9em;white-space:nowrap;';
        btn.onclick=function(){window.location.reload();};
        d.appendChild(btn);
        var close=document.createElement('button');
        close.textContent='\u00D7';
        close.style.cssText='background:none;border:none;color:#999;cursor:pointer;font-size:1.3em;padding:0 0.2em;line-height:1;';
        close.onclick=function(){ftDismiss(d);};
        d.appendChild(close);
        document.body.appendChild(d);
    }
    function ftShowToast(){
        if(document.getElementById('ft-config-toast'))return;
        var d=document.createElement('div');
        d.id='ft-config-toast';
        d.style.cssText='position:fixed;bottom:1.5em;left:50%;transform:translateX(-50%);z-index:10000;background:#1e1e1e;color:#eee;border:1px solid rgba(255,255,255,0.15);border-radius:8px;padding:0.8em 1.2em;display:flex;align-items:center;gap:1em;font-family:inherit;font-size:0.95em;box-shadow:0 4px 20px rgba(0,0,0,0.5);';
        var txt=document.createElement('span');
        txt.textContent='Plugin settings changed. Refresh to apply.';
        d.appendChild(txt);
        var btn=document.createElement('button');
        btn.textContent='Refresh';
        btn.style.cssText='background:#00a4dc;color:#fff;border:none;border-radius:4px;padding:0.4em 1em;cursor:pointer;font-size:0.9em;white-space:nowrap;';
        btn.onclick=function(){window.location.reload();};
        d.appendChild(btn);
        var close=document.createElement('button');
        close.textContent='\u00D7';
        close.style.cssText='background:none;border:none;color:#999;cursor:pointer;font-size:1.3em;padding:0 0.2em;line-height:1;';
        close.onclick=function(){ftDismiss(d);};
        d.appendChild(close);
        document.body.appendChild(d);
    }
    function ftStart(){
        setInterval(function(){
            if(document.hidden)return;
            ftCheck();
        },5000);
        window.addEventListener('hashchange',function(){setTimeout(ftCheck,500);});
        window.addEventListener('focus',function(){setTimeout(ftCheck,500);});
        document.addEventListener('visibilitychange',function(){if(!document.hidden)setTimeout(ftCheck,500);});
    }
    if(document.readyState==='loading'){
        document.addEventListener('DOMContentLoaded',ftStart);
    }else{
        ftStart();
    }
})();
</script>
