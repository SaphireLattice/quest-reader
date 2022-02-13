await (async () => {
    delete Array.prototype.toJSON;

    const processReply = (elem) => {
        const id = +elem.querySelector(":scope > .postwidth > a[name]:not([name=s])").getAttribute("name");
        const title = elem.querySelector(":scope > .postwidth .filetitle")?.innerText.trim();
        const author = elem.querySelector(":scope > .postwidth .postername").innerText.trim();
        const uid = elem.querySelector(":scope > .postwidth .uid").innerText.replace("ID: ", "");
        const file = elem.querySelector(":scope > .postwidth > .filesize > a")?.href ?? undefined;
        const postertrip = elem.querySelector(":scope > .postwidth .postertrip")?.innerText.trim();
        const rawHtml = elem.querySelector(":scope > blockquote").innerHTML
            .replace(`<div style="display:inline-block; width:400px;"></div><br>`,"")
            .trim();
        const date = [...elem.querySelector(":scope > .postwidth > label").childNodes]
            .pop().data.trim()
            .replace(
                /(\d{4,4})\/(\d\d)\/(\d\d)\(\w+\)(\d\d):(\d\d)/,
                "$1-$2-$3T$4:$5:00Z"
            )
            .replace(
                /href=\\"\/kusaba\/questarch\/res\/\d+.html#\d+\\" onclick=\\"return highlight\('\d+', true\);\\"/,
                ""
            );

        const filenameRaw = elem.querySelector(":scope > .postwidth > .filesize")?.innerText;
        const filename = filenameRaw?.includes("File ") ?
        	filenameRaw.match(/File \d+\.[^ ]+ - \([\d\.KMG]+B , \d+x\d+ , (.*) \)/)[1]
        	?? undefined : undefined;
        const ret = {
            id,
            author,
            uid,
            rawHtml,
            date
        }
        if (file) ret.file = file;
        if (file) ret.filename = filename;
        if (postertrip) ret.tripcode = postertrip;
        if (title) ret.title = title;
        return ret;
    }

    const replies = [...document.getElementsByClassName("reply")];
    replies.unshift(document.getElementById("delform"))

    const processed = replies.map(elem => processReply(elem));

    const blob = new Blob(
        [JSON.stringify(processed, null, 4)],
        {type : 'application/json'}
   	)

    const a = document.createElement("a");
    const url = URL.createObjectURL(blob);
    a.href = url;
    a.download = `thread_${processed[0].id}.json`;
    document.body.appendChild(a);
    a.click();
    URL.revokeObjectURL(url);
    a.remove();


    return ;

})();